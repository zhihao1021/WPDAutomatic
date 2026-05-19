using System.Text.Json.Serialization;
using Interop.UIAutomationClient;

namespace WPDAutomatic.Models;

public sealed class ElementNode {
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("automationId")]
    public string AutomationId { get; init; } = string.Empty;

    [JsonPropertyName("controlType")]
    public string ControlType { get; init; } = string.Empty;

    [JsonPropertyName("className")]
    public string ClassName { get; init; } = string.Empty;

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; init; }

    [JsonPropertyName("isOffscreen")]
    public bool IsOffscreen { get; init; }

    [JsonPropertyName("boundingRectangle")]
    public BoundingRectangle? BoundingRectangle { get; init; }

    [JsonPropertyName("helpText")]
    public string HelpText { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("isPassword")]
    public bool IsPassword { get; init; }

    [JsonPropertyName("frameworkId")]
    public string FrameworkId { get; init; } = string.Empty;

    [JsonPropertyName("processId")]
    public int ProcessId { get; init; }

    [JsonPropertyName("runtimeId")]
    public string RuntimeId { get; init; } = string.Empty;

    [JsonPropertyName("patterns")]
    public List<string> Patterns { get; init; } = [];

    [JsonPropertyName("children")]
    public List<ElementNode> Children { get; init; } = [];

    [JsonIgnore]
    public IUIAutomationElement? BackingElement { get; init; }
}

public sealed class BoundingRectangle {
    [JsonPropertyName("x")]
    public double X { get; init; }

    [JsonPropertyName("y")]
    public double Y { get; init; }

    [JsonPropertyName("width")]
    public double Width { get; init; }

    [JsonPropertyName("height")]
    public double Height { get; init; }
}
