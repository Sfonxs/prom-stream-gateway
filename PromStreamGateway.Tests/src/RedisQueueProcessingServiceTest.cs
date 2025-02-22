using Xunit;
using StackExchange.Redis;

public class RedisQueueProcessingServiceTest : IClassFixture<MainTestFixture>
{  
    private readonly ConnectionMultiplexer _redis;

    public RedisQueueProcessingServiceTest(MainTestFixture fixture)
    {
        _redis = ConnectionMultiplexer.Connect(fixture.RedisConnectionString);
    }

    [Fact]
    public async Task Can_Set_And_Get_Value_From_Redis()
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync("test_key", "hello");
        
        var value = await db.StringGetAsync("test_key");
        Assert.Equal("hello", value.ToString());
    }
}
