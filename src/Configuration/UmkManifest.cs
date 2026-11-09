using System.Text.Json.Serialization;

namespace UMK.Configuration;

public sealed class UmkManifest
{
    [JsonPropertyName("umk")]
    public string Umk { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("shell")]
    public string Shell { get; set; } = string.Empty;

    [JsonPropertyName("variables")]
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("scripts")]
    public Dictionary<string, List<string>> Scripts { get; set; } = new(StringComparer.Ordinal);
}
