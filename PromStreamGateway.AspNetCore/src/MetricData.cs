using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

public class MetricData
{
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]  // Ensures type is serialized as a string
    public MetricType Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;  // Ensures `name` is never null

    [JsonPropertyName("help")]
    public string Help { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; set; }  // Nullable if not present

    /// <summary>
    /// Validates that `name` is not empty.
    /// </summary>
    private bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Name);
    }

    /// <summary>
    /// Tries to deserialize and validate the JSON.
    /// </summary>
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