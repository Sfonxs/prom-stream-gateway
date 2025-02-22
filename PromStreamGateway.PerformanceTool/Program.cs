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
        const int spamRate = 1000;
        const int workerCount = 4;

        Console.WriteLine($"🔹 Connecting to Redis at: {redisConnectionString}");
        _redis = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
        _db = _redis.GetDatabase();

        Console.WriteLine($"🚀 Spamming {spamRate} metrics per second with {workerCount} workers...");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) => { 
            eventArgs.Cancel = true; 
            cts.Cancel(); 
        };

        // Start worker tasks
        var tasks = new List<Task>();
        for (int i = 0; i < workerCount; i++)
        {
            tasks.Add(Task.Run(() => SpamMetrics(spamRate / workerCount, cts.Token)));
        }

        await Task.WhenAll(tasks);
    }

    private static async Task SpamMetrics(int messagesPerSecond, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var metric = GenerateRandomMetric();
            string json = JsonSerializer.Serialize(metric);
            await _db.ListLeftPushAsync(_queueKey, json);

            await Task.Delay(1000 / messagesPerSecond, cancellationToken);
        }
    }

    private static object GenerateRandomMetric()
    {
        var metricTypes = new[] { "counter", "gauge", "histogram", "summary" };
        var metricType = metricTypes[_random.Next(metricTypes.Length)];

        return new
        {
            type = metricType,
            name = $"test_metric_{_random.Next(1, 100)}",
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
