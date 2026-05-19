using System.Text.Json;
using WPDAutomatic.Core;
using WPDAutomatic.Models;

namespace WPDAutomatic.Mcp;

public sealed class ToolDefinition {
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public object InputSchema { get; init; } = new { type = "object", properties = new { } };
    public Func<Dictionary<string, JsonElement>, AutomationService, string> Execute { get; init; } = (_, _) => "";
}

internal static class UiaToolDefinitions {
    public static Dictionary<string, ToolDefinition> GetTools() {
        var tools = new List<ToolDefinition>
        {
            CreateTool(
                "uia_list_processes",
                "List running Windows processes that have a visible window. Use this to find the target application PID before attaching.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        filter = new
                        {
                            type = "string",
                            description = "Optional filter to match process name or window title"
                        }
                    }
                },
                (args, svc) =>
                {
                    var filter = GetString(args, "filter");
                    var processes = svc.ListProcesses(filter);
                    if (processes.Count == 0) return "No processes with visible windows found.";
                    return string.Join("\n", processes.Select(p =>
                        $"PID={p.ProcessId} | {p.ProcessName} | \"{p.MainWindowTitle}\" | HWND={p.MainWindowHandle}"));
                }
            ),
            CreateTool(
                "uia_attach",
                "Attach UIA to a target process by its PID. Must be called before other UIA operations on that process.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        processId = new
                        {
                            type = "integer",
                            description = "The process ID (PID) of the target application"
                        }
                    },
                    required = new[] { "processId" }
                },
                (args, svc) =>
                {
                    var pid = GetInt(args, "processId");
                    var ok = svc.AttachToProcess(pid);
                    return ok ? $"Attached to process {pid}." : $"Failed to attach to process {pid}.";
                }
            ),
            CreateTool(
                "uia_get_tree",
                "Get the full UI Automation element tree with properties, patterns, and runtimeIds. Use this to discover the UI layout before interacting.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        maxDepth = new
                        {
                            type = "integer",
                            description = "Maximum depth of tree traversal (default: 4)",
                            @default = 4
                        },
                        runtimeId = new
                        {
                            type = "string",
                            description = "Optional runtime ID to get subtree of a specific element"
                        }
                    }
                },
                (args, svc) =>
                {
                    var maxDepth = GetInt(args, "maxDepth", 4);
                    var runtimeId = GetString(args, "runtimeId");

                    ElementNode? root = null;
                    if (!string.IsNullOrEmpty(runtimeId))
                    {
                        var node = svc.FindElementByRuntimeId(runtimeId);
                        root = node;
                    }

                    var tree = svc.GetElementTree(root?.BackingElement, maxDepth);
                    return tree is null ? "No elements found." : FormatTree(tree, 0);
                }
            ),
            CreateTool(
                "uia_find",
                "Find the first UI element matching search criteria. Returns element details including runtimeId for subsequent operations.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Element name (e.g. 'OK', 'Submit')" },
                        automationId = new { type = "string", description = "Automation ID (e.g. 'btnSubmit')" },
                        className = new { type = "string", description = "Class name (e.g. 'Button', 'Edit')" },
                        controlType = new { type = "string", description = "Control type: Button, Edit, ComboBox, CheckBox, ListItem, MenuItem, TabItem, TreeItem, Window, Pane, etc." },
                        isEnabled = new { type = "boolean", description = "Filter by enabled/disabled state" },
                        searchDescendants = new { type = "boolean", description = "Search descendants recursively (default: true)", @default = true },
                        maxDepth = new { type = "integer", description = "Max search depth (default: 5)", @default = 5 }
                    }
                },
                (args, svc) =>
                {
                    var criteria = BuildCriteria(args);
                    if (criteria.IsEmpty) return "Error: at least one search criterion is required.";

                    var result = svc.FindFirst(criteria);
                    return result is null ? "No matching element found." : FormatElement(result);
                }
            ),
            CreateTool(
                "uia_find_all",
                "Find all UI elements matching search criteria. Use this to count or enumerate all matching elements.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Element name to match" },
                        automationId = new { type = "string", description = "Automation ID to match" },
                        className = new { type = "string", description = "Class name to match" },
                        controlType = new { type = "string", description = "Control type to match" },
                        isEnabled = new { type = "boolean", description = "Filter by enabled/disabled state" },
                        searchDescendants = new { type = "boolean", description = "Search descendants recursively (default: true)", @default = true },
                        maxDepth = new { type = "integer", description = "Max search depth (default: 5)", @default = 5 }
                    }
                },
                (args, svc) =>
                {
                    var criteria = BuildCriteria(args);
                    var results = svc.FindElements(criteria);
                    if (results.Count == 0) return "No matching elements found.";

                    return $"Found {results.Count} element(s):\n" +
                           string.Join("\n", results.Select((e, i) => $"[{i}] {FormatElementInline(e)}"));
                }
            ),
            CreateTool(
                "uia_click",
                "Click a UI element by its runtimeId. Falls back to search criteria if runtimeId not provided.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        runtimeId = new { type = "string", description = "Runtime ID of the element to click (preferred)" },
                        name = new { type = "string", description = "Fallback: element name to search and click" },
                        controlType = new { type = "string", description = "Fallback: control type to narrow search" }
                    }
                },
                (args, svc) =>
                {
                    var element = ResolveElement(svc, args, "click");
                    svc.Click(element);
                    return $"Clicked element '{element.Name}' successfully.";
                }
            ),
            CreateTool(
                "uia_double_click",
                "Double-click a UI element by its runtimeId.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        runtimeId = new { type = "string", description = "Runtime ID of the element" }
                    },
                    required = new[] { "runtimeId" }
                },
                (args, svc) =>
                {
                    var element = ResolveByRuntimeId(svc, args);
                    svc.DoubleClick(element);
                    return $"Double-clicked element '{element.Name}' successfully.";
                }
            ),
            CreateTool(
                "uia_right_click",
                "Right-click a UI element by its runtimeId.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        runtimeId = new { type = "string", description = "Runtime ID of the element" }
                    },
                    required = new[] { "runtimeId" }
                },
                (args, svc) =>
                {
                    var element = ResolveByRuntimeId(svc, args);
                    svc.RightClick(element);
                    return $"Right-clicked element '{element.Name}' successfully.";
                }
            ),
            CreateTool(
                "uia_type",
                "Type text into an input field (Edit control) identified by its runtimeId. Use this to fill text boxes.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        runtimeId = new { type = "string", description = "Runtime ID of the text input element" },
                        value = new { type = "string", description = "The text to type/input" }
                    },
                    required = new[] { "runtimeId", "value" }
                },
                (args, svc) =>
                {
                    var element = ResolveByRuntimeId(svc, args);
                    var value = GetString(args, "value") ?? "";
                    svc.SetValue(element, value);
                    return $"Typed \"{value}\" into element '{element.Name}'.";
                }
            ),
            CreateTool(
                "uia_invoke",
                "Invoke a UI element (typically a Button). Uses InvokePattern which is the most reliable way to press buttons.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        runtimeId = new { type = "string", description = "Runtime ID of the element to invoke" }
                    },
                    required = new[] { "runtimeId" }
                },
                (args, svc) =>
                {
                    var element = ResolveByRuntimeId(svc, args);
                    svc.Invoke(element);
                    return $"Invoked element '{element.Name}'.";
                }
            ),
            CreateTool(
                "uia_select",
                "Select an item from a ComboBox, ListBox, or similar control. Provide the parent element runtimeId and item name.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        runtimeId = new { type = "string", description = "Runtime ID of the ComboBox/List control" },
                        item = new { type = "string", description = "Name/text of the item to select" }
                    },
                    required = new[] { "runtimeId", "item" }
                },
                (args, svc) =>
                {
                    var element = ResolveByRuntimeId(svc, args);
                    var item = GetString(args, "item") ?? "";
                    svc.SelectItem(element, item);
                    return $"Selected \"{item}\" from '{element.Name}'.";
                }
            ),
            CreateTool(
                "uia_toggle",
                "Toggle a CheckBox or other toggle element by its runtimeId.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        runtimeId = new { type = "string", description = "Runtime ID of the toggle element" }
                    },
                    required = new[] { "runtimeId" }
                },
                (args, svc) =>
                {
                    var element = ResolveByRuntimeId(svc, args);
                    svc.Toggle(element);
                    return $"Toggled element '{element.Name}'.";
                }
            ),
            CreateTool(
                "uia_expand",
                "Expand a tree node, combo box, or other expandable element.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        runtimeId = new { type = "string", description = "Runtime ID of the element to expand" }
                    },
                    required = new[] { "runtimeId" }
                },
                (args, svc) =>
                {
                    var element = ResolveByRuntimeId(svc, args);
                    svc.Expand(element);
                    return $"Expanded element '{element.Name}'.";
                }
            ),
            CreateTool(
                "uia_collapse",
                "Collapse a tree node, combo box, or other collapsible element.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        runtimeId = new { type = "string", description = "Runtime ID of the element to collapse" }
                    },
                    required = new[] { "runtimeId" }
                },
                (args, svc) =>
                {
                    var element = ResolveByRuntimeId(svc, args);
                    svc.Collapse(element);
                    return $"Collapsed element '{element.Name}'.";
                }
            ),
            CreateTool(
                "uia_get_property",
                "Read a specific property from a UI element by its runtimeId.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        runtimeId = new { type = "string", description = "Runtime ID of the element" },
                        property = new
                        {
                            type = "string",
                            description = "Property name: Name, AutomationId, ClassName, ControlType, IsEnabled, IsOffscreen, BoundingRectangle, HelpText, FrameworkId, ProcessId, IsPassword, Value, AcceleratorKey, AccessKey, ItemStatus, Orientation, NativeWindowHandle, IsKeyboardFocusable, HasKeyboardFocus, ItemType, AriaRole"
                        }
                    },
                    required = new[] { "runtimeId", "property" }
                },
                (args, svc) =>
                {
                    var element = ResolveByRuntimeId(svc, args);
                    var prop = GetString(args, "property") ?? "Name";
                    var val = svc.GetPropertyValue(element, prop);
                    return $"{prop} = {val}";
                }
            ),
            CreateTool(
                "uia_get_focused",
                "Get the currently focused/active UI element. Use this to discover which control has keyboard focus.",
                new
                {
                    type = "object",
                    properties = new { }
                },
                (args, svc) =>
                {
                    var element = svc.GetFocusedElement();
                    return element is null ? "No focused element found." : FormatElement(element);
                }
            ),
            CreateTool(
                "uia_get_patterns",
                "Get the list of UI Automation patterns supported by an element (Invoke, Value, Toggle, ExpandCollapse, etc.).",
                new
                {
                    type = "object",
                    properties = new
                    {
                        runtimeId = new { type = "string", description = "Runtime ID of the element" }
                    },
                    required = new[] { "runtimeId" }
                },
                (args, svc) =>
                {
                    var element = ResolveByRuntimeId(svc, args);
                    var patterns = svc.GetSupportedPatterns(element);
                    return string.Join(", ", patterns);
                }
            ),
            CreateTool(
                "uia_wait_for_element",
                "Wait for a UI element matching criteria to appear within a timeout. Useful for waiting on dialogs or async UI updates.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Element name to wait for" },
                        automationId = new { type = "string", description = "Automation ID to wait for" },
                        controlType = new { type = "string", description = "Control type to wait for" },
                        timeoutMs = new { type = "integer", description = "Timeout in milliseconds (default: 5000)", @default = 5000 }
                    }
                },
                (args, svc) =>
                {
                    var criteria = BuildCriteria(args);
                    var timeout = GetInt(args, "timeoutMs", 5000);
                    var found = svc.WaitForElement(criteria, timeout);
                    return found
                        ? $"Element found within {timeout}ms."
                        : $"Element NOT found within {timeout}ms.";
                }
            ),
            CreateTool(
                "uia_scroll_into_view",
                "Scroll a UI element into the visible area of its container.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        runtimeId = new { type = "string", description = "Runtime ID of the element" }
                    },
                    required = new[] { "runtimeId" }
                },
                (args, svc) =>
                {
                    var element = ResolveByRuntimeId(svc, args);
                    svc.ScrollIntoView(element);
                    return $"Scrolled '{element.Name}' into view.";
                }
            ),
        };

        return tools.ToDictionary(t => t.Name);
    }

    private static ToolDefinition CreateTool(
        string name, string description, object inputSchema,
        Func<Dictionary<string, JsonElement>, AutomationService, string> execute) {
        return new ToolDefinition {
            Name = name,
            Description = description,
            InputSchema = inputSchema,
            Execute = execute
        };
    }

    private static SearchCriteria BuildCriteria(Dictionary<string, JsonElement> args) {
        return new SearchCriteria {
            Name = GetString(args, "name"),
            AutomationId = GetString(args, "automationId"),
            ClassName = GetString(args, "className"),
            ControlType = GetString(args, "controlType"),
            IsEnabled = GetBool(args, "isEnabled"),
            SearchDescendants = GetBool(args, "searchDescendants") ?? true,
            MaxDepth = GetInt(args, "maxDepth", 5),
        };
    }

    private static ElementNode ResolveElement(AutomationService svc, Dictionary<string, JsonElement> args, string action) {
        var runtimeId = GetString(args, "runtimeId");
        if (!string.IsNullOrEmpty(runtimeId)) {
            var node = svc.FindElementByRuntimeId(runtimeId);
            if (node is not null) return node;
        }

        var criteria = BuildCriteria(args);
        if (!criteria.IsEmpty) {
            var node = svc.FindFirst(criteria);
            if (node is not null) return node;
        }

        throw new InvalidOperationException(
            $"Cannot resolve element for '{action}'. Provide runtimeId or search criteria.");
    }

    private static ElementNode ResolveByRuntimeId(AutomationService svc, Dictionary<string, JsonElement> args) {
        var runtimeId = GetString(args, "runtimeId");
        if (string.IsNullOrEmpty(runtimeId))
            throw new InvalidOperationException("runtimeId is required.");

        var node = svc.FindElementByRuntimeId(runtimeId);
        if (node is null)
            throw new InvalidOperationException($"Element with runtimeId '{runtimeId}' not found.");

        return node;
    }

    private static string? GetString(Dictionary<string, JsonElement> args, string key) {
        if (args.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString();
        return null;
    }

    private static int GetInt(Dictionary<string, JsonElement> args, string key, int defaultValue = 0) {
        if (args.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.GetInt32();
        return defaultValue;
    }

    private static bool? GetBool(Dictionary<string, JsonElement> args, string key) {
        if (args.TryGetValue(key, out var el) &&
            (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False))
            return el.GetBoolean();
        return null;
    }

    private static string FormatElement(ElementNode el) {
        return $"--- Element ---\n" +
               $"Name: {el.Name}\n" +
               $"AutomationId: {el.AutomationId}\n" +
               $"ControlType: {el.ControlType}\n" +
               $"ClassName: {el.ClassName}\n" +
               $"RuntimeId: {el.RuntimeId}\n" +
               $"IsEnabled: {el.IsEnabled}\n" +
               $"IsOffscreen: {el.IsOffscreen}\n" +
               $"IsPassword: {el.IsPassword}\n" +
               $"FrameworkId: {el.FrameworkId}\n" +
               $"ProcessId: {el.ProcessId}\n" +
               $"Value: {el.Value ?? "(none)"}\n" +
               $"HelpText: {el.HelpText}\n" +
               $"BoundingRect: {(el.BoundingRectangle is { } r ? $"x={r.X},y={r.Y},w={r.Width},h={r.Height}" : "(none)")}\n" +
               $"Patterns: [{string.Join(", ", el.Patterns)}]\n" +
               $"Children: {el.Children.Count}";
    }

    private static string FormatElementInline(ElementNode el) {
        return $"{el.ControlType} \"{el.Name}\" rid:{el.RuntimeId} enabled:{el.IsEnabled}";
    }

    private static string FormatTree(ElementNode node, int depth) {
        var indent = new string(' ', depth * 2);
        var patterns = node.Patterns.Count > 0 ? $" [{string.Join(",", node.Patterns)}]" : "";
        var suffix = !node.IsEnabled ? " (DISABLED)" : node.IsOffscreen ? " (OFFSCREEN)" : "";
        var line = $"{indent}{node.ControlType} \"{node.Name}\" rid:{node.RuntimeId}{suffix}{patterns}";

        if (node.Children.Count == 0)
            return line;

        return line + "\n" + string.Join("\n", node.Children.Select(c => FormatTree(c, depth + 1)));
    }
}
