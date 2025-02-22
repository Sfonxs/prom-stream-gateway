public class ConsumeRedisQueueProcessingHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ConsumeRedisQueueProcessingHostedService> _logger;

    public ConsumeRedisQueueProcessingHostedService(
        IServiceProvider services,
        ILogger<ConsumeRedisQueueProcessingHostedService> logger
    )
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hosted service running.");

        await DoWork(stoppingToken);
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hosted service is working.");

        using (var scope = _services.CreateScope())
        {
            var scopedProcessingService =
                scope.ServiceProvider
                    .GetRequiredService<IRedisQueueProcessingService>();

            await scopedProcessingService.DoWork(stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hosted service is stopping.");

        await base.StopAsync(stoppingToken);
    }
}