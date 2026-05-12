using System.Diagnostics;
using Microsoft.Extensions.Options;
using Prometheus;
using StackExchange.Redis;

internal class RedisQueueProcessingService : IRedisQueueProcessingService
{
    private readonly ILogger _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly MetricFactory _metricFactory;
    private readonly RedisOptions _redisOptions;
    private readonly MetricOptions _metricOptions;

    public RedisQueueProcessingService(
        ILogger<RedisQueueProcessingService> logger,
        IConnectionMultiplexer redis,
        MetricFactory metricFactory,
        IOptions<RedisOptions> redisOptions,
        IOptions<MetricOptions> metricOptions
    )
    {
        _logger = logger;
        _redis = redis;
        _metricFactory = metricFactory;
        _redisOptions = redisOptions.Value;
        _metricOptions = metricOptions.Value;
    }

    public async Task DoWork(CancellationToken stoppingToken, int workerIdx)
    {
        var database = _redis.GetDatabase(_redisOptions.MetricQueueDatabase);
        var metricQueueKey = new RedisKey(_redisOptions.MetricQueueKey);


        Prometheus.Counter.Child? emptyPopCounter = null;
        Prometheus.Gauge.Child? pendingQueueSize = null;
        Prometheus.Counter.Child? droppedMetricsCounter = null;
        Prometheus.Counter.Child? processedMetricsCounter = null;

        if (!_metricOptions.DisableMetaMetrics)
        {
            emptyPopCounter = _metricFactory
                .CreateCounter(
                    "prom_stream_gateway_redis_queue_empty_pops_total",
                    "Number of times this Prom Stream Gateway worker called RPOP on the Redis metric queue and got nothing back, then slept 100ms before retrying. " +
                    "A high-and-growing rate across all workers means the workers are mostly idle and you can safely lower Redis.MetricQueueWorkers; a rate near zero on every worker means the queue is consistently saturated and the gateway may be falling behind ingestion (compare against prom_stream_gateway_redis_queue_pending_size). " +
                    "Labels: worker = ordinal index of the background worker (0..Redis.MetricQueueWorkers-1); queueKey = Redis list key being drained (Redis.MetricQueueKey).",
                    new CounterConfiguration { LabelNames = ["worker", "queueKey"] })
                .WithLabels(workerIdx.ToString(), _redisOptions.MetricQueueKey);
            pendingQueueSize = _metricFactory
                .CreateGauge(
                    "prom_stream_gateway_redis_queue_pending_size",
                    "Current LLEN of the Redis metric queue, sampled by every Prom Stream Gateway worker once per second. " +
                    "This is the live backlog of metric JSON payloads that producers (e.g. AWS Lambda invocations) have LPUSHed but no worker has RPOPed yet. " +
                    "A non-zero, growing value means producers are publishing faster than the worker pool can drain, so aggregated metrics on /metrics will lag behind reality. " +
                    "Sustained growth is the primary signal that the gateway needs more workers (Redis.MetricQueueWorkers), a larger pop batch (Redis.MetricQueuePopCount), or that downstream Prometheus is scraping too slowly. " +
                    "Label: queueKey = Redis list key being measured (Redis.MetricQueueKey).",
                    new GaugeConfiguration { LabelNames = ["queueKey"], SuppressInitialValue = _metricOptions.DisableMetaMetrics })
                .WithLabels(_redisOptions.MetricQueueKey);
            droppedMetricsCounter = _metricFactory
                .CreateCounter(
                    "prom_stream_gateway_dropped_metrics_total",
                    "Number of metric payloads this Prom Stream Gateway worker discarded without aggregating them. " +
                    "A drop happens for one of two reasons: (1) the JSON failed MetricData.TryParse validation (malformed JSON, unknown 'type', missing required fields, etc.) — these are logged at Warning with the parse reason and the raw JSON; or (2) the per-metric aggregation threw an unexpected exception (e.g. label cardinality conflict with an existing series of the same name). " +
                    "Any non-zero value here represents producer-side data loss for that metric sample; investigate the worker logs to find the offending payloads. " +
                    "Labels: worker = ordinal index of the background worker; queueKey = Redis list key the dropped payload was popped from.",
                    new CounterConfiguration { LabelNames = ["worker", "queueKey"] })
                .WithLabels(workerIdx.ToString(), _redisOptions.MetricQueueKey);
            processedMetricsCounter = _metricFactory
                .CreateCounter(
                    "prom_stream_gateway_processed_metrics_total",
                    "Number of metric payloads this Prom Stream Gateway worker successfully parsed and applied to the in-memory Prometheus registry that backs /metrics. " +
                    "Sum across workers gives the total ingestion throughput of the gateway; use rate() to see metrics-per-second and compare to prom_stream_gateway_redis_queue_pending_size to detect whether throughput is keeping up with producer load. " +
                    "Labels: worker = ordinal index of the background worker; queueKey = Redis list key the payload was popped from.",
                    new CounterConfiguration { LabelNames = ["worker", "queueKey"] })
                .WithLabels(workerIdx.ToString(), _redisOptions.MetricQueueKey);
        }

        var sw = Stopwatch.StartNew();
        var pendingQueueMeasureInterval = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            if (sw.Elapsed > pendingQueueMeasureInterval)
            {
                sw.Restart();
                pendingQueueSize?.Set(await database.ListLengthAsync(metricQueueKey));
            }

            var rawMetrics = await database.ListRightPopAsync(metricQueueKey, _redisOptions.MetricQueuePopCount);
            if (rawMetrics == null || rawMetrics.Length == 0)
            {
                emptyPopCounter?.Inc();
                await Task.Delay(100, stoppingToken);
                continue;
            }

            var workerExceptions = new List<Exception>();
            foreach (var rawMetric in rawMetrics)
            {
                try
                {
                    var rawMetricString = rawMetric.ToString();
                    if (!MetricData.TryParse(rawMetricString, out var metric, out var reason))
                    {
                        droppedMetricsCounter?.Inc();
                        _logger.LogWarning("Dropped invalid metric because \"{Reason}\": {RawJson}", reason, rawMetricString);
                    }
                    else
                    {
                        ProcessMetric(metric);
                        processedMetricsCounter?.Inc();
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning("Dropped metric because of unexpected worker exception: {Message}", e.Message);
                    workerExceptions.Add(e);
                    droppedMetricsCounter?.Inc();
                }
            }
            if (workerExceptions.Count != 0)
            {
                throw new AggregateException(workerExceptions);
            }
        }
    }

    private void ProcessMetric(MetricData metric)
    {
        var labels = metric.Labels ?? [];

        if (_metricOptions.SortIncomingLabels)
        {
            labels = labels
                .OrderBy(kv => kv.Key)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        var labelNames = labels.Keys.ToArray();
        var labelValues = labels.Values.ToArray();

        switch (metric.Type)
        {
            case MetricType.Counter:
                var counter = _metricFactory
                    .CreateCounter(metric.Name, metric.Help, new CounterConfiguration { LabelNames = labelNames })
                    .WithLabels(labelValues);
                counter.Inc(metric.Value);
                break;

            case MetricType.Gauge:
                var gauge = _metricFactory
                    .CreateGauge(metric.Name, metric.Help, new GaugeConfiguration { LabelNames = labelNames })
                    .WithLabels(labelValues);
                gauge.Set(metric.Value);
                break;

            case MetricType.Histogram:
                var histogram = _metricFactory
                    .CreateHistogram(metric.Name, metric.Help, new HistogramConfiguration { LabelNames = labelNames, Buckets = metric.Buckets ?? [] })
                    .WithLabels(labelValues);
                histogram.Observe(metric.Value);
                break;

            case MetricType.Summary:
                var quantiles = metric.Quantiles ?? [];
                var epsilons = metric.Epsilons ?? [];
                var objectives = quantiles.Zip(epsilons, (q, e) => new QuantileEpsilonPair(q, e)).ToArray();

                var summary = _metricFactory
                    .CreateSummary(metric.Name, metric.Help, new SummaryConfiguration { LabelNames = labelNames, Objectives = objectives })
                    .WithLabels(labelValues);
                summary.Observe(metric.Value);
                break;
        }
    }
}