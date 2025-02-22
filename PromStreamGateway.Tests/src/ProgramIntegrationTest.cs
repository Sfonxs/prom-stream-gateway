using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

public class ProgramIntegrationTest : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly IDatabase _redis;
    private readonly string _queueKey;

    public ProgramIntegrationTest(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        var redisOptions = factory.Services.GetRequiredService<IOptions<RedisOptions>>().Value;
        _queueKey = redisOptions.MetricQueueKey;
        _redis = factory.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase(redisOptions.MetricQueueDatabase);
    }

    [Fact]
    public async Task Metrics_Endpoint_Returns_Ingested_Counter_Metrics()
    {
        await EnqueueAsync(new
        {
            type = "counter",
            name = "simple_counter",
            value = 1,
            labels = new
            {
                instance = "some-instance",
                job = "some-job",
                tenant = "some-tenant-id"
            }
        }, 11);

        await WaitUntilQueueProcessedAsync();

        var metrics = await GetMetricsAsync();
        Assert.Contains("# HELP simple_counter", metrics);
        Assert.Contains("# TYPE simple_counter counter", metrics);
        Assert.Contains("simple_counter{instance=\"some-instance\",job=\"some-job\",tenant=\"some-tenant-id\"} 11", metrics);
    }

    [Fact]
    public async Task Metrics_Endpoint_Returns_Ingested_Histogram_Metrics()
    {
        await EnqueueAsync(new
        {
            type = "histogram",
            name = "simple_histrogram",
            help = "Some help text.",
            value = 0.75,  // Simulating request duration
            labels = new
            {
                method = "GET",
                endpoint = "/api/data"
            },
            buckets = new double[] { 0.1, 0.5, 1, 5, 10 }
        }, 4);

        await WaitUntilQueueProcessedAsync();

        var metrics = await GetMetricsAsync();

        Assert.Contains("simple_histrogram_sum{endpoint=\"/api/data\",method=\"GET\"} 3", metrics);
        Assert.Contains("simple_histrogram_count{endpoint=\"/api/data\",method=\"GET\"} 4", metrics);
        Assert.Contains("simple_histrogram_bucket{endpoint=\"/api/data\",method=\"GET\",le=\"+Inf\"} 4", metrics);
        Assert.Contains("simple_histrogram_bucket{endpoint=\"/api/data\",method=\"GET\",le=\"10\"} 4", metrics);
        Assert.Contains("simple_histrogram_bucket{endpoint=\"/api/data\",method=\"GET\",le=\"5\"} 4", metrics);
        Assert.Contains("simple_histrogram_bucket{endpoint=\"/api/data\",method=\"GET\",le=\"1\"} 4", metrics);
        Assert.Contains("simple_histrogram_bucket{endpoint=\"/api/data\",method=\"GET\",le=\"0.5\"} 0", metrics);
        Assert.Contains("simple_histrogram_bucket{endpoint=\"/api/data\",method=\"GET\",le=\"0.1\"} 0", metrics);
    }


    [Fact]
    public async Task Metrics_Endpoint_Returns_Ingested_Histogram_Metrics_Different_Buckets_Ignores_Later_Buckets()
    {
        await EnqueueAsync(new
        {
            type = "histogram",
            name = "simple_histrogram_different_buckets",
            value = 5,  // Simulating request duration
            buckets = new double[] { 1, 10}
        }, 4);
        await WaitUntilQueueProcessedAsync();
        await EnqueueAsync(new
        {
            type = "histogram",
            name = "simple_histrogram_different_buckets",
            value = 5,  // Simulating request duration
            buckets = new double[] { 4, 6, 7}
        }, 4);
        await WaitUntilQueueProcessedAsync();

        var metrics = await GetMetricsAsync();

        Assert.Contains("simple_histrogram_different_buckets_sum 40", metrics);
        Assert.Contains("simple_histrogram_different_buckets_count 8", metrics);
        Assert.Contains("simple_histrogram_different_buckets_bucket{le=\"+Inf\"} 8", metrics);
        Assert.Contains("simple_histrogram_different_buckets_bucket{le=\"10\"} 8", metrics);
        Assert.Contains("simple_histrogram_different_buckets_bucket{le=\"1\"} 0", metrics);
        Assert.DoesNotContain("simple_histrogram_different_buckets_bucket{le=\"7\"}", metrics);
        Assert.DoesNotContain("simple_histrogram_different_buckets_bucket{le=\"6\"}", metrics);
        Assert.DoesNotContain("simple_histrogram_different_buckets_bucket{le=\"4\"}", metrics);
    }


    [Fact]
    public async Task Metrics_Endpoint_Returns_Ingested_Histogram_Metrics_With_No_Buckets_Is_Dropped()
    {
        await EnqueueAsync(new
        {
            type = "histogram",
            name = "simple_histrogram_no_buckets",
            help = "Some help text.",
            value = 0.75,  // Simulating request duration
            labels = new
            {
                method = "GET",
                endpoint = "/api/data"
            }
        }, 4);

        await WaitUntilQueueProcessedAsync();

        var metrics = await GetMetricsAsync();

        Assert.DoesNotContain("simple_histrogram_no_buckets_sum", metrics);
    }

    [Fact]
    public async Task Metrics_Endpoint_Returns_Ingested_Gauge_Metrics()
    {
        await EnqueueAsync(new
        {
            type = "gauge",
            name = "simple_gauge",
            value = 36.7,
            labels = new
            {
                sensor = "room-1",
                unit = "celsius"
            }
        }, 1);

        await WaitUntilQueueProcessedAsync();

        var metrics = await GetMetricsAsync();
        Assert.Contains("# HELP simple_gauge", metrics);
        Assert.Contains("# TYPE simple_gauge gauge", metrics);
        Assert.Contains("simple_gauge{sensor=\"room-1\",unit=\"celsius\"} 36.7", metrics);
    }

    [Fact]
    public async Task Metrics_Endpoint_Returns_Ingested_Summary_Metrics()
    {
        await EnqueueAsync(new
        {
            type = "summary",
            name = "simple_summary",
            value = 512,
            labels = new
            {
                method = "POST",
                endpoint = "/upload"
            },
            quantiles = new double[] { 0.5, 0.9, 0.99 },
            epsilons = new double[] { 0.05, 0.01, 0.001 }
        }, 2);

        await WaitUntilQueueProcessedAsync();

        var metrics = await GetMetricsAsync();
        Assert.Contains("# HELP simple_summary", metrics);
        Assert.Contains("# TYPE simple_summary summary", metrics);
        Assert.Contains("simple_summary_sum{endpoint=\"/upload\",method=\"POST\"} 1024", metrics);
        Assert.Contains("simple_summary_count{endpoint=\"/upload\",method=\"POST\"} 2", metrics);
        Assert.Contains("simple_summary{endpoint=\"/upload\",method=\"POST\",quantile=\"0.5\"} 512", metrics);
        Assert.Contains("simple_summary{endpoint=\"/upload\",method=\"POST\",quantile=\"0.9\"} 512", metrics);
        Assert.Contains("simple_summary{endpoint=\"/upload\",method=\"POST\",quantile=\"0.99\"} 512", metrics);
    }

    [Fact]
    public async Task Metrics_Endpoint_Returns_Ingested_Summary_Metrics_Without_Quantiles_Works()
    {
        await EnqueueAsync(new
        {
            type = "summary",
            name = "simple_summary_no_quantiles",
            value = 512,
            labels = new
            {
                method = "POST",
                endpoint = "/upload"
            },
        }, 2);

        await WaitUntilQueueProcessedAsync();

        var metrics = await GetMetricsAsync();
        Assert.Contains("# HELP simple_summary_no_quantiles", metrics);
        Assert.Contains("# TYPE simple_summary_no_quantiles summary", metrics);
        Assert.Contains("simple_summary_no_quantiles_sum{endpoint=\"/upload\",method=\"POST\"} 1024", metrics);
        Assert.Contains("simple_summary_no_quantiles_count{endpoint=\"/upload\",method=\"POST\"} 2", metrics);
    }

    [Fact]
    public async Task Invalid_Json_Is_Safely_Ignored()
    {
        await _redis.ListLeftPushAsync(_queueKey, "{invalid_json");

        await WaitUntilQueueProcessedAsync();

        var metrics = await GetMetricsAsync();
        Assert.DoesNotContain("invalid_json", metrics);
    }

    [Fact]
    public async Task Metrics_With_Invalid_Type_Are_Dropped()
    {
        await EnqueueAsync(new
        {
            type = "unknown_type",
            name = "invalid_metric",
            value = 5,
            labels = new
            {
                source = "test"
            }
        }, 1);

        await WaitUntilQueueProcessedAsync();

        var metrics = await GetMetricsAsync();
        Assert.DoesNotContain("invalid_metric", metrics);
    }

    [Fact]
    public async Task Large_Number_Of_Metrics_Are_Processed()
    {
        await EnqueueAsync(new
        {
            type = "counter",
            name = "bulk_metrics_counter",
            value = 1,
            labels = new
            {
                batch = "test"
            }
        }, 1000);

        await WaitUntilQueueProcessedAsync();

        var metrics = await GetMetricsAsync();
        Assert.Contains("bulk_metrics_counter{batch=\"test\"} 1000", metrics);
    }

    [Fact]
    public async Task Metrics_With_Empty_Labels_Are_Handled_Correctly()
    {
        await EnqueueAsync(new
        {
            type = "counter",
            name = "metric_without_labels",
            value = 5,
            labels = new { } // Empty labels
        }, 1);
        await EnqueueAsync(new
        {
            type = "counter",
            name = "metric_with_undefined_labels",
            value = 5,
        }, 1);

        await WaitUntilQueueProcessedAsync();

        var metrics = await GetMetricsAsync();
        Assert.Contains("metric_without_labels 5", metrics);
        Assert.Contains("metric_with_undefined_labels 5", metrics);
    }

    [Fact]
    public async Task Counter_Metrics_With_Different_Label_Names_Work()
    {
        await EnqueueAsync(new
        {
            type = "counter",
            name = "different_label_names_counter",
            value = 1,
            labels = new
            {
                instance = "some-instance",
                job = "some-job",
                tenant = "some-tenant-id"
            }
        }, 5);
        await EnqueueAsync(new
        {
            type = "counter",
            name = "different_label_names_counter",
            value = 1,
            labels = new
            {
                external = "some-external",
                tenant = "some-tenant-id",
                component = "some-component",
            }
        }, 3);

        await WaitUntilQueueProcessedAsync();

        var metrics = await GetMetricsAsync();
        Assert.Contains("# HELP different_label_names_counter", metrics);
        Assert.Contains("# TYPE different_label_names_counter counter", metrics);
        Assert.Contains("different_label_names_counter{instance=\"some-instance\",job=\"some-job\",tenant=\"some-tenant-id\"} 5", metrics);
        Assert.Contains("different_label_names_counter{component=\"some-component\",external=\"some-external\",tenant=\"some-tenant-id\"} 3", metrics);
    }

    [Fact]
    public async Task Same_Metric_Name_But_Different_Type_Is_Dropped()
    {
        await EnqueueAsync(new
        {
            type = "counter",
            name = "simple_counter_and_gauge",
            value = 1,
            labels = new
            {
                instance = "some-instance",
                job = "some-job",
                tenant = "some-tenant-id"
            }
        }, 1);
        await WaitUntilQueueProcessedAsync();

        await EnqueueAsync(new
        {
            type = "gauge",
            name = "simple_counter_and_gauge",
            value = 1,
            labels = new
            {
                instance = "some-instance",
                job = "some-job",
                tenant = "some-tenant-id"
            }
        }, 1);
        await WaitUntilQueueProcessedAsync();

        var metrics = await GetMetricsAsync();
        Assert.Contains("# HELP simple_counter", metrics);
        Assert.Contains("# TYPE simple_counter counter", metrics);
        Assert.Contains("simple_counter{instance=\"some-instance\",job=\"some-job\",tenant=\"some-tenant-id\"} 11", metrics);
    }

    private async Task EnqueueAsync(dynamic metric, int times)
    {
        for (int i = 0; i < times; i++)
        {
            await _redis.ListLeftPushAsync(_queueKey, JsonSerializer.Serialize(metric));
        }
    }

    private async Task WaitUntilQueueProcessedAsync()
    {
        await Task.Delay(500); // Allow the workers to process the queue... (this should be improved...)
    }

    private async Task<string> GetMetricsAsync()
    {
        var response = await _client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return content;
    }
}
