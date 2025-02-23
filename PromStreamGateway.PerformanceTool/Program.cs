using System.Diagnostics;
using System.Text.Json;
using StackExchange.Redis;

class Program
{
    private static ConnectionMultiplexer? _redis;
    private static IDatabase? _db;
    private static readonly string _queueKey = "prom-stream-gateway:metric-queue";
    private static readonly Random _random = new();

    static async Task Main(string[] args)
    {
        const string redisConnectionString = "localhost:6379";
        const int ratePerSecond = 10_000;
        const int workerCount = 10;

        Console.WriteLine($"🔹 Connecting to Redis at: {redisConnectionString}");
        _redis = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
        _db = _redis.GetDatabase();

        Console.WriteLine($"🚀 Spamming {ratePerSecond} metrics per second with {workerCount} workers...");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        // Start worker tasks
        var tasks = new List<Task>();
        for (int i = 0; i < workerCount; i++)
        {
            tasks.Add(Task.Run(() => SpamMetrics(ratePerSecond / workerCount, cts.Token)));
        }

        await Task.WhenAll(tasks);
    }

    private static async Task SpamMetrics(int messagesPerSecond, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        while (!cancellationToken.IsCancellationRequested)
        {
            for (int i = 0; i < messagesPerSecond; i++)
            {
                var metric = GenerateRandomMetric();
                string json = JsonSerializer.Serialize(metric);
                await _db.ListLeftPushAsync(_queueKey, json);
            }

            var elapsed = sw.Elapsed;
            var remaining = TimeSpan.FromSeconds(1) - elapsed;
            sw.Restart();
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining);
            }
            else
            {
                Console.WriteLine("Worker can not keep up with the configured messages per second: " + messagesPerSecond + ". Current speed: " + (messagesPerSecond / elapsed.TotalSeconds));
            }
        }
    }

    private static object GenerateRandomMetric()
    {
        var metricTypes = new[] { "counter", "gauge", "histogram", "summary" };
        var metricType = metricTypes[_random.Next(metricTypes.Length)];

        return new
        {
            type = metricType,
            name = $"test_metric_{_random.Next(1, 100)}_{metricType}",
            value = _random.NextDouble() * 100,
            labels = new Dictionary<string, string>
            {
                { "instance", $"instance_{_random.Next(1, 10)}" },
                { "job", "load_test" },
                { "region", $"region_{_random.Next(1, 5)}" }
            },
            buckets = metricType == "histogram" ? new double[] { 0.1, 1, 5, 10, 50 } : null,
            quantiles = metricType == "summary" ? new double[] { 0.5, 0.9, 0.99 } : null,
            epsilons = metricType == "summary" ? new double[] { 0.01, 0.005, 0.001 } : null
        };
    }
}
