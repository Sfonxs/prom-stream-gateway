using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

public class ProgramIntegrationTest  : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProgramIntegrationTest(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Metrics_Endpoint_Should_Return_Valid_Prometheus_Output()
    {
        // Wait a bit for the hosted service to start.
        await Task.Delay(1000);

        var response = await _client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("prom_stream_gateway_redis_queue_pops_total", content);
        Assert.Contains("prom_stream_gateway_redis_queue_empty_pops_total", content);
    }
}