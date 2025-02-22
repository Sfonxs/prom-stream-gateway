using System;
using System.Threading.Tasks;
using Testcontainers.Redis;
using Xunit;

public class MainTestFixture : IAsyncLifetime
{
    public RedisContainer RedisContainer { get; private set; } = new RedisBuilder().WithImage("redis:latest").Build();
    public string RedisConnectionString => RedisContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await RedisContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await RedisContainer.DisposeAsync();
    }
}
