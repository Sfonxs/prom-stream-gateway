internal interface IRedisQueueProcessingService
{
    Task DoWork(CancellationToken stoppingToken);
}