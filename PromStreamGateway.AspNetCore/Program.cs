
using Prometheus;
using StackExchange.Redis;

// Setup the metrics collector.
var customRegistry = Metrics.NewCustomRegistry();
var myCounter = Metrics.WithCustomRegistry(customRegistry)
    .CreateCounter("my_custom_metric", "This is my custom metric.", new CounterConfiguration
    {
        LabelNames = ["method"]
    });

// Start creating the web app.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(provider => ConnectionMultiplexer.Connect("localhost"));
builder.Services.AddSingleton(provider => Metrics.WithCustomRegistry(customRegistry));
builder.Services.AddSingleton<IHostedService, ConsumeRedisQueueProcessingHostedService>();
builder.Services.AddScoped<IRedisQueueProcessingService, RedisQueueProcessingService>();

var app = builder.Build();

app.UseRouting();

app.UseMetricServer((settings) =>
{
    settings.Registry = customRegistry;
});

app.MapGet("/", () =>
{
    myCounter.WithLabels("GET").Inc();
    return "ok";
});

app.Run();