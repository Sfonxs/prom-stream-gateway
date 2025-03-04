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
                .CreateCounter("prom_stream_gateway_redis_queue_empty_pops_total", "", new CounterConfiguration { LabelNames = ["worker", "queueKey"] })
                .WithLabels(workerIdx.ToString(), _redisOptions.MetricQueueKey);
            pendingQueueSize = _metricFactory
                .CreateGauge("prom_stream_gateway_redis_queue_pending_size", "", new GaugeConfiguration { LabelNames = ["queueKey"], SuppressInitialValue = _metricOptions.DisableMetaMetrics })
                .WithLabels(_redisOptions.MetricQueueKey);
            droppedMetricsCounter = _metricFactory
                .CreateCounter("prom_stream_gateway_dropped_metrics_total", "", new CounterConfiguration { LabelNames = ["worker", "queueKey"] })
                .WithLabels(workerIdx.ToString(), _redisOptions.MetricQueueKey);
            processedMetricsCounter = _metricFactory
                .CreateCounter("prom_stream_gateway_processed_metrics_total", "", new CounterConfiguration { LabelNames = ["worker", "queueKey"] })
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