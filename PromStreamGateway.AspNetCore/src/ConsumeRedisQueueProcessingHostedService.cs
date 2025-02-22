using Microsoft.Extensions.Options;

public class ConsumeRedisQueueProcessingHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ConsumeRedisQueueProcessingHostedService> _logger;
    private readonly int _workerCount;
    private Task[]? _workers;
    private readonly CancellationTokenSource _cts = new();

    public ConsumeRedisQueueProcessingHostedService(
        IServiceProvider services,
         ILogger<ConsumeRedisQueueProcessingHostedService> logger,
         IOptions<RedisOptions> redisOptions
         )
    {
        _services = services;
        _logger = logger;
        _workerCount = redisOptions.Value.MetricQueueWorkers;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting {WorkerCount} metric queue workers...", _workerCount);

        _workers = new Task[_workerCount];
        for (int i = 0; i < _workerCount; i++)
        {
            int workerIdx = i;
            _workers[i] = Task.Run(() => DoWork(_cts.Token, workerIdx), _cts.Token);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping metric queue workers...");
        _cts.Cancel();

        if (_workers != null)
        {
            await Task.WhenAll(_workers);
        }
    }

    private async Task DoWork(CancellationToken stoppingToken, int workerIdx)
    {
        _logger.LogInformation("Worker {WorkerIdx} is starting.", workerIdx);

        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _services.CreateScope())
            {
                var scopedProcessingService = scope.ServiceProvider.GetRequiredService<IRedisQueueProcessingService>();

                try
                {
                    await scopedProcessingService.DoWork(stoppingToken, workerIdx);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in worker {WorkerIdx}.", workerIdx);
                }
            }

            await Task.Delay(1000, stoppingToken);
        }

        _logger.LogInformation("Worker {WorkerIdx} is stopping.", workerIdx);
    }
}