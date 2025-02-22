
using Prometheus;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var app = builder.Build();

var customRegistry = Metrics.NewCustomRegistry();

var myCounter = Metrics.WithCustomRegistry(customRegistry)
    .CreateCounter("my_custom_metric", "This is my custom metric.", new CounterConfiguration
    {
        LabelNames = ["method"]
    });

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