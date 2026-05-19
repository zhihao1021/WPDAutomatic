using System.Text.Json.Serialization;

namespace WPDAutomatic.Models;

public sealed class Command
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, object?> Parameters { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
