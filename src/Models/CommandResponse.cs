using System.Text.Json.Serialization;

namespace WPDAutomatic.Models;

public sealed class CommandResponse {
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public object? Data { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("elapsedMs")]
    public long ElapsedMs { get; init; }

    public static CommandResponse Ok(string id, string action, object? data, long elapsedMs)
        => new() { Id = id, Success = true, Action = action, Data = data, ElapsedMs = elapsedMs };

    public static CommandResponse Fail(string id, string action, string error, long elapsedMs)
        => new() { Id = id, Success = false, Action = action, Error = error, ElapsedMs = elapsedMs };
}
