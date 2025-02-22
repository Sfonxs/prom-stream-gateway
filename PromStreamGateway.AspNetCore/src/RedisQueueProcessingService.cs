using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using Prometheus;
using StackExchange.Redis;

internal class RedisQueueProcessingService : IRedisQueueProcessingService
{
    private readonly ILogger _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly MetricFactory _metricFactory;
    private readonly RedisOptions _redisOptions;

    public RedisQueueProcessingService(
        ILogger<RedisQueueProcessingService> logger,
        IConnectionMultiplexer redis,
        MetricFactory metricFactory,
        IOptions<RedisOptions> redisOptions
    )
    {
        _logger = logger;
        _redis = redis;
        _metricFactory = metricFactory;
        _redisOptions = redisOptions.Value;
    }

    public async Task DoWork(CancellationToken stoppingToken, int workerIdx)
    {
        var database = _redis.GetDatabase(_redisOptions.MetricQueueDatabase);
        var metricQueueKey = new RedisKey(this._redisOptions.MetricQueueKey);

        var nonEmptyPopCount = _metricFactory
            .CreateCounter("prom_stream_gateway_redis_queue_pops_total", "", new CounterConfiguration { LabelNames = ["worker"] })
            .WithLabels(workerIdx.ToString());
        var emptyPopCounter = _metricFactory
            .CreateCounter("prom_stream_gateway_redis_queue_empty_pops_total", "", new CounterConfiguration { LabelNames = ["worker"] })
            .WithLabels(workerIdx.ToString());
        var droppedMetricsCounter = _metricFactory
            .CreateCounter("prom_stream_gateway_dropped_metrics_total", "", new CounterConfiguration { LabelNames = ["worker"] })
            .WithLabels(workerIdx.ToString());
        var processedMetricsCounter = _metricFactory
            .CreateCounter("prom_stream_gateway_processed_metrics_total", "", new CounterConfiguration { LabelNames = ["worker"] })
            .WithLabels(workerIdx.ToString());

        while (!stoppingToken.IsCancellationRequested)
        {
            var rawMetric = await database.ListRightPopAsync(metricQueueKey);
            if (!rawMetric.HasValue)
            {
                emptyPopCounter.Inc();
                await Task.Delay(100, stoppingToken);
                continue;
            }
            else
            {
                nonEmptyPopCount.Inc();
            }

            if (!MetricData.TryParse(rawMetric.ToString(), out var metric))
            {
                droppedMetricsCounter.Inc();
                continue;
            }

            try
            {
                ProcessMetric(metric);
                processedMetricsCounter.Inc();
            }
            catch
            {
                droppedMetricsCounter.Inc();
                throw;
            }
        }
    }

    private void ProcessMetric(MetricData metric)
    {
        var labels = metric.Labels ?? [];
        var labelNames = labels.Keys.ToArray();
        var labelValues = labels.Values.ToArray();

        switch (metric.Type)
        {
            case MetricType.Counter:
                var counter = _metricFactory
                    .CreateCounter(metric.Name, metric.Help, new CounterConfiguration { LabelNames = labelNames })
                    .WithLabels(labelValues);
                counter.Inc(metric.Value);  // Increment counter by the metric value
                break;

            case MetricType.Gauge:
                var gauge = _metricFactory
                    .CreateGauge(metric.Name, metric.Help, new GaugeConfiguration { LabelNames = labelNames })
                    .WithLabels(labelValues);
                gauge.Set(metric.Value);  // Set the gauge value
                break;

            case MetricType.Histogram:
                var histogram = _metricFactory
                    .CreateHistogram(metric.Name, metric.Help, new HistogramConfiguration { LabelNames = labelNames })
                    .WithLabels(labelValues);
                histogram.Observe(metric.Value);  // Observe the metric value
                break;

            case MetricType.Summary:
                var summary = _metricFactory
                    .CreateSummary(metric.Name, metric.Help, new SummaryConfiguration { LabelNames = labelNames })
                    .WithLabels(labelValues);
                summary.Observe(metric.Value);  // Observe the metric value
                break;
        }
    }
}