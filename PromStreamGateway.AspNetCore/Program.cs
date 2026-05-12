
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
    var connectionString = redisOptions.ConnectionString;
    if (!connectionString.Contains("abortConnect=false", StringComparison.OrdinalIgnoreCase))
    {
        connectionString += ",abortConnect=false";
    }
    return ConnectionMultiplexer.Connect(connectionString);
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

// Self-documenting info metric. Always present (independent of Metrics.DisableMetaMetrics) so any
// consumer scraping /metrics — including AI agents — can read the full service description from the
// HELP line without needing the README.
var metricFactory = app.Services.GetRequiredService<MetricFactory>();
metricFactory
    .CreateGauge(
        "prom_stream_gateway_info",
        "Prom Stream Gateway: a self-hosted Prometheus aggregation gateway. " +
        "Producers (typically short-lived stateless workloads such as AWS Lambda invocations) LPUSH metric samples as JSON onto a Redis list (default key prom-stream-gateway:metric-queue, configurable via Redis.MetricQueueKey). " +
        "A pool of background workers (Redis.MetricQueueWorkers) RPOP batches of size Redis.MetricQueuePopCount, parse each JSON sample, and increment / observe / set the corresponding Counter, Gauge, Histogram, or Summary in this process's in-memory Prometheus registry. " +
        "Sleep is 100ms whenever a worker pops an empty batch. The aggregated state is then exposed at /metrics for Prometheus to scrape. " +
        "This decouples short-lived stateless producers from Prometheus's pull model and avoids the per-invocation pre-aggregation cost of the official Push/Aggregation Gateway. " +
        "Unlike the official Aggregation Gateway, gauge samples replace the previous value (rather than being summed). " +
        "All other prom_stream_gateway_* metrics on this endpoint describe the health and throughput of that ingestion pipeline (set Metrics.DisableMetaMetrics=true to suppress them). " +
        "Every other metric exposed by this service was forwarded by a producer; its name, type, labels, and HELP come from the producer, not from this gateway. " +
        "Always 1.")
    .Set(1);

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