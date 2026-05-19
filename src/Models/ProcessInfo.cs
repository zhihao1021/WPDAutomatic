using System.Text.Json.Serialization;

namespace WPDAutomatic.Models;

public sealed class ProcessInfo
{
    [JsonPropertyName("processId")]
    public int ProcessId { get; init; }

    [JsonPropertyName("processName")]
    public string ProcessName { get; init; } = string.Empty;

    [JsonPropertyName("mainWindowTitle")]
    public string MainWindowTitle { get; init; } = string.Empty;

    [JsonPropertyName("mainWindowHandle")]
    public long MainWindowHandle { get; init; }

    [JsonPropertyName("hasUi")]
    public bool HasUi { get; init; }
}
