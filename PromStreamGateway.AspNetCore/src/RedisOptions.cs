public class RedisOptions
{
    public string MetricQueueKey { get; set; } = string.Empty;
    public int MetricQueueWorkers { get; set; }
    public int MetricQueueDatabase { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
}