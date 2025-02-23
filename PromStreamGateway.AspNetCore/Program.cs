
using Microsoft.Extensions.Options;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Configure redis client
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection("Redis"));
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var redisOptions = provider.GetRequiredService<IOptions<RedisOptions>>().Value;
    return ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
});

// Configure prometheus SDK
builder.Services.AddSingleton(provider => Metrics.NewCustomRegistry());
builder.Services.AddSingleton(provider =>
{
    var factory = Metrics.WithCustomRegistry(provider.GetRequiredService<CollectorRegistry>());
    factory.ExemplarBehavior = ExemplarBehavior.NoExemplars();
    return factory;
});

// Configure the background workers
builder.Services.Configure<MetricOptions>(builder.Configuration.GetSection("Metrics"));
builder.Services.AddSingleton<IHostedService, ConsumeRedisQueueProcessingHostedService>();
builder.Services.AddScoped<IRedisQueueProcessingService, RedisQueueProcessingService>();

var app = builder.Build();

app.UseRouting();
app.UseMetricServer((settings) =>
{
    settings.Registry = app.Services.GetRequiredService<CollectorRegistry>();
});
app.MapGet("/", () =>
{
    return "ok";
});

app.Run();

// Workaround for the tests.
public partial class Program { }