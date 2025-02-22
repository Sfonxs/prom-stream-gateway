using Prometheus;
using StackExchange.Redis;

internal class RedisQueueProcessingService : IRedisQueueProcessingService
{
    private readonly ILogger _logger;
    private readonly ConnectionMultiplexer _redis;
    private readonly MetricFactory _metricFactory;
    
    public RedisQueueProcessingService(
        ILogger<RedisQueueProcessingService> logger,
        ConnectionMultiplexer redis,
        MetricFactory metricFactory
        )
    {
        _logger = logger;
        _redis = redis;
        _metricFactory = metricFactory;
    }

    public async Task DoWork(CancellationToken stoppingToken)
    {
        var database = _redis.GetDatabase();
        var executionCountKey = new RedisKey("execution-count");
        database.StringSet(executionCountKey, 0);

        var gauge = _metricFactory.CreateGauge("execution_count_gauge", "");
        var counter =  _metricFactory.CreateCounter("execution_count_counter", "");

        while (!stoppingToken.IsCancellationRequested)
        {
            var executionCount = database.StringIncrement(executionCountKey);

            gauge.IncTo(executionCount);
            counter.Inc();

            await Task.Delay(100, stoppingToken);
        }
    }
}