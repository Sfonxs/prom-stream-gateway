using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

public class MetricData
{
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MetricType Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("help")]
    public string Help { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; set; }

    [JsonPropertyName("buckets")]
    public double[]? Buckets { get; set; }

    [JsonPropertyName("quantiles")]
    public double[]? Quantiles { get; set; }

    [JsonPropertyName("epsilons")]
    public double[]? Epsilons { get; set; }

    private bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Name);
    }

    public static bool TryParse(string json, [NotNullWhen(true)] out MetricData? metric)
    {
        try
        {
            metric = JsonSerializer.Deserialize<MetricData>(json);
            return metric != null && metric.IsValid();
        }
        catch (JsonException)
        {
            metric = null;
            return false;
        }
    }
}