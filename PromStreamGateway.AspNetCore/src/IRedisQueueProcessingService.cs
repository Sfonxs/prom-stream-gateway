internal interface IRedisQueueProcessingService
{
    Task DoWork(CancellationToken stoppingToken, int workerIdx);
}