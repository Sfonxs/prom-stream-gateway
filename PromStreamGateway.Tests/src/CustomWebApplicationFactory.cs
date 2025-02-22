using System;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly RedisTestFixture _redisFixture = new();

    public string RedisConnectionString => _redisFixture.RedisConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var configuration = new Dictionary<string, string?>
            {
                { "Redis:ConnectionString", RedisConnectionString } // Override Redis connection
            };

            config.AddInMemoryCollection(configuration);
        });

        builder.ConfigureServices(services =>
        {
            // Replace the real Redis connection with our test Redis
            var descriptor = services.SingleOrDefault(s => s.ServiceType == typeof(IConnectionMultiplexer));
            if (descriptor != null) services.Remove(descriptor);

            services.AddSingleton<IConnectionMultiplexer>(provider =>
                ConnectionMultiplexer.Connect(RedisConnectionString));
        });
    }

    async Task IAsyncLifetime.InitializeAsync() => await _redisFixture.InitializeAsync();
    async Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();  // Stops the WebApplicationFactory (shuts down the app)

        await _redisFixture.DisposeAsync();  // Now safely shut down Redis
    }
}
