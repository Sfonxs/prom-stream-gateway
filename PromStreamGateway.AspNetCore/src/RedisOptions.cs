public class RedisOptions
{
    public string MetricQueueKey { get; set; } = string.Empty;
    public int MetricQueueWorkers {get;set;} = 0;
    public string ConnectionString { get; set; } = string.Empty;
}