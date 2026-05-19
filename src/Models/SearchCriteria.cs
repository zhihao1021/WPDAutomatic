namespace WPDAutomatic.Models;

public sealed class SearchCriteria {
    public string? Name { get; init; }
    public string? AutomationId { get; init; }
    public string? ClassName { get; init; }
    public string? ControlType { get; init; }
    public string? FrameworkId { get; init; }
    public bool? IsEnabled { get; init; }
    public int? ProcessId { get; init; }
    public int? MaxDepth { get; init; }
    public bool? SearchDescendants { get; init; }

    public bool IsEmpty =>
        Name is null &&
        AutomationId is null &&
        ClassName is null &&
        ControlType is null &&
        FrameworkId is null &&
        IsEnabled is null &&
        ProcessId is null;

    public override string ToString() {
        var parts = new List<string>();
        if (Name != null) parts.Add($"Name={Name}");
        if (AutomationId != null) parts.Add($"AutomationId={AutomationId}");
        if (ClassName != null) parts.Add($"ClassName={ClassName}");
        if (ControlType != null) parts.Add($"ControlType={ControlType}");
        if (FrameworkId != null) parts.Add($"FrameworkId={FrameworkId}");
        if (IsEnabled.HasValue) parts.Add($"IsEnabled={IsEnabled}");
        if (ProcessId.HasValue) parts.Add($"ProcessId={ProcessId}");
        return parts.Count > 0 ? string.Join(", ", parts) : "(any)";
    }
}
