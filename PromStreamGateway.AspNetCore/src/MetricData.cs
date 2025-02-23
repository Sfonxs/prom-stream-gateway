using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(MetricData))]
internal partial class MetricDataJsonContext : JsonSerializerContext { }

public class MetricData
{
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter<MetricType>))]
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

    private static ConcurrentDictionary<string, MetricType> _metricNameToType = [];

    private bool IsValid(out string reason)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            reason = "Name is required.";
            return false;
        }

        if (Type == MetricType.Summary)
        {
            if (
                (Quantiles != null && Epsilons == null)
                || (Quantiles == null && Epsilons != null)
                || (Quantiles?.Length != Epsilons?.Length)
            )
            {
                reason = "Summaries require quantiles and epsilons counts to be the same.";
                return false;
            }
        }


        if (Type == MetricType.Histogram)
        {
            if (Buckets == null || Buckets.Length == 0)
            {
                reason = "Histograms require at least one bucket.";
                return false;
            }
        }

        if (_metricNameToType.GetOrAdd(Name, Type) != Type)
        {
            reason = "Metric name was already seen under a different metric type.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public static bool TryParse(string json, [NotNullWhen(true)] out MetricData? metric, out string reason)
    {
        reason = string.Empty;
        try
        {
            metric = JsonSerializer.Deserialize(json, MetricDataJsonContext.Default.MetricData);
            return metric != null && metric.IsValid(out reason);
        }
        catch (JsonException)
        {
            metric = null;
            reason = "Invalid json.";
            return false;
        }
    }
}