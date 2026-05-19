using System.Text.Json;
using System.Text.Json.Nodes;
using WPDAutomatic.Core;
using WPDAutomatic.Models;

namespace WPDAutomatic.Mcp;

public sealed class McpServer : IDisposable {
    private readonly AutomationService _automation;
    private readonly Dictionary<string, ToolDefinition> _tools;

    public McpServer() {
        _automation = new AutomationService();
        _tools = UiaToolDefinitions.GetTools();
    }

    public async Task RunAsync(CancellationToken ct = default) {
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

        string? line;
        while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync(ct)) is not null) {
            if (string.IsNullOrWhiteSpace(line)) continue;

            JsonNode? request;
            try {
                request = JsonNode.Parse(line);
            }
            catch {
                SendError(writer, null, -32700, "Parse error", null);
                continue;
            }

            var method = request?["method"]?.GetValue<string>();
            var id = request?["id"];

            if (method is null) {
                continue; // Skip responses/notifications without method
            }

            switch (method) {
                case "initialize":
                    HandleInitialize(writer, id);
                    break;
                case "notifications/initialized":
                    // Client is ready
                    break;
                case "ping":
                    SendResult(writer, id, new { });
                    break;
                case "tools/list":
                    HandleToolsList(writer, id);
                    break;
                case "tools/call":
                    HandleToolsCall(writer, id, request?["params"]);
                    break;
                default:
                    SendError(writer, id, -32601, $"Method not found: {method}", null);
                    break;
            }
        }
    }

    private static void HandleInitialize(StreamWriter writer, JsonNode? id) {
        var result = new {
            protocolVersion = "2024-11-05",
            capabilities = new {
                tools = new { }
            },
            serverInfo = new {
                name = "WPDAutomatic",
                version = "1.0.0"
            }
        };
        SendResult(writer, id, result);
    }

    private void HandleToolsList(StreamWriter writer, JsonNode? id) {
        var tools = _tools.Values.Select(t => new {
            name = t.Name,
            description = t.Description,
            inputSchema = t.InputSchema
        }).ToList();

        var result = new { tools };
        SendResult(writer, id, result);
    }

    private void HandleToolsCall(StreamWriter writer, JsonNode? id, JsonNode? paramsNode) {
        var toolName = paramsNode?["name"]?.GetValue<string>();
        var argsNode = paramsNode?["arguments"];

        if (toolName is null || !_tools.TryGetValue(toolName, out var tool)) {
            SendError(writer, id, -32602, $"Unknown tool: {toolName}", null);
            return;
        }

        try {
            var args = argsNode is not null
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsNode.ToJsonString())
                : [];

            var result = tool.Execute(args ?? [], _automation);
            var content = new[]
            {
                new
                {
                    type = "text",
                    text = result
                }
            };

            SendResult(writer, id, new { content });
        }
        catch (Exception ex) {
            SendError(writer, id, -32000, $"Tool execution failed: {ex.Message}", null);
        }
    }

    private static void SendResult(StreamWriter writer, JsonNode? id, object result) {
        var response = new Dictionary<string, object?> {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.GetValue<object>(),
            ["result"] = result
        };
        writer.WriteLine(JsonSerializer.Serialize(response));
    }

    private static void SendError(StreamWriter writer, JsonNode? id, int code, string message, object? data) {
        var error = new Dictionary<string, object?> {
            ["code"] = code,
            ["message"] = message
        };
        if (data is not null)
            error["data"] = data;

        var response = new Dictionary<string, object?> {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.GetValue<object>(),
            ["error"] = error
        };
        writer.WriteLine(JsonSerializer.Serialize(response));
    }

    public void Dispose() {
        _automation.Dispose();
    }
}
