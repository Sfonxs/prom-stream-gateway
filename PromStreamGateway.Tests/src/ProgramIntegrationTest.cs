using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

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
    public async Task Metrics_Endpoint_Should_Return_Ingested_Metrics_From_Redis_Queue()
    {
        var value = JsonSerializer.Serialize(new
        {
            type = "counter",
            name = "some_name",
            value = 1,
            labels = new {
                instance = "some-instance",
                job = "some-job",
                tenant = "some-tenant-id"
            }
        });
        for(int i = 0; i < 11; i++) {
            await _redis.ListLeftPushAsync(_queueKey, value);
        }
        await Task.Delay(1000); // Allow the workers to process the queue... (this should be improved)

        var response = await _client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("# HELP some_name", content);
        Assert.Contains("# TYPE some_name counter", content);
        Assert.Contains("some_name{instance=\"some-instance\",job=\"some-job\",tenant=\"some-tenant-id\"} 11", content);
    }
}