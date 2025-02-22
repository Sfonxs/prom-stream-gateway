using Microsoft.Extensions.Options;
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
        var database = _redis.GetDatabase();
        var metricQueueKey = new RedisKey(this._redisOptions.MetricQueueKey);

        var nonEmptyPopCount = _metricFactory.CreateCounter("prom_stream_gateway_redis_queue_pops_total", "", new CounterConfiguration { LabelNames = ["worker"] }).WithLabels(workerIdx.ToString());
        var emptyPopCounter = _metricFactory.CreateCounter("prom_stream_gateway_redis_queue_empty_pops_total", "", new CounterConfiguration { LabelNames = ["worker"] }).WithLabels(workerIdx.ToString());

        while (!stoppingToken.IsCancellationRequested)
        {
            var metric = await database.ListRightPopAsync(metricQueueKey);
            if(!metric.HasValue) {
                emptyPopCounter.Inc();
                await Task.Delay(100, stoppingToken);
                continue;
            }
            
            nonEmptyPopCount.Inc();
        }
    }
}