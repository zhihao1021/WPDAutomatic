using System.Text.Json;
using System.Text.Json.Serialization;
using Interop.UIAutomationClient;
using WPDAutomatic.Core;
using WPDAutomatic.Models;

namespace WPDAutomatic.Commands;

public sealed class CommandRouter {
    private readonly AutomationService _automation;
    private readonly JsonSerializerOptions _jsonOptions;

    public CommandRouter() {
        _automation = new AutomationService();
        _jsonOptions = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
        };
    }

    public string ProcessCommand(string line) {
        Command command;
        try {
            command = JsonSerializer.Deserialize<Command>(line, _jsonOptions)
                ?? throw new InvalidOperationException("Deserialized command is null");
        }
        catch (Exception ex) {
            return SerializeResponse(CommandResponse.Fail("unknown", "parse_error", $"Failed to parse command: {ex.Message}", 0));
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try {
            return ExecuteCommand(command, sw);
        }
        catch (Exception ex) {
            sw.Stop();
            return SerializeResponse(CommandResponse.Fail(command.Id, command.Action, ex.Message, sw.ElapsedMilliseconds));
        }
    }

    private string ExecuteCommand(Command command, System.Diagnostics.Stopwatch sw) {
        return command.Action.ToUpperInvariant() switch {
            "LIST_PROCESSES" => HandleListProcesses(command, sw),
            "ATTACH" => HandleAttach(command, sw),
            "ATTACH_WINDOW" => HandleAttachWindow(command, sw),
            "DETACH" => HandleDetach(command, sw),
            "GET_TREE" => HandleGetTree(command, sw),
            "FIND" => HandleFind(command, sw),
            "FIND_FIRST" => HandleFindFirst(command, sw),
            "CLICK" => HandleClick(command, sw),
            "DOUBLE_CLICK" => HandleDoubleClick(command, sw),
            "RIGHT_CLICK" => HandleRightClick(command, sw),
            "SET_VALUE" => HandleSetValue(command, sw),
            "INVOKE" => HandleInvoke(command, sw),
            "TOGGLE" => HandleToggle(command, sw),
            "SELECT_ITEM" => HandleSelectItem(command, sw),
            "EXPAND" => HandleExpand(command, sw),
            "COLLAPSE" => HandleCollapse(command, sw),
            "SCROLL_INTO_VIEW" => HandleScrollIntoView(command, sw),
            "GET_PROPERTY" => HandleGetProperty(command, sw),
            "GET_PATTERNS" => HandleGetPatterns(command, sw),
            "GET_FOCUSED_ELEMENT" => HandleGetFocusedElement(command, sw),
            "ELEMENT_FROM_POINT" => HandleElementFromPoint(command, sw),
            "WAIT_FOR_ELEMENT" => HandleWaitForElement(command, sw),
            "QUIT" => HandleQuit(command, sw),
            _ => SerializeResponse(CommandResponse.Fail(command.Id, command.Action, $"Unknown action: {command.Action}", sw.ElapsedMilliseconds))
        };
    }

    private string HandleListProcesses(Command cmd, System.Diagnostics.Stopwatch sw) {
        var filter = GetParam<string?>(cmd, "filter");
        var processes = _automation.ListProcesses(filter);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, processes, sw.ElapsedMilliseconds));
    }

    private string HandleAttach(Command cmd, System.Diagnostics.Stopwatch sw) {
        var processId = GetParam<int>(cmd, "processId");
        var success = _automation.AttachToProcess(processId);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { attached = success }, sw.ElapsedMilliseconds));
    }

    private string HandleAttachWindow(Command cmd, System.Diagnostics.Stopwatch sw) {
        var handle = GetParam<long>(cmd, "handle");
        var success = _automation.AttachToWindow((IntPtr)handle);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { attached = success }, sw.ElapsedMilliseconds));
    }

    private string HandleDetach(Command cmd, System.Diagnostics.Stopwatch sw) {
        _automation.Detach();
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { detached = true }, sw.ElapsedMilliseconds));
    }

    private string HandleGetTree(Command cmd, System.Diagnostics.Stopwatch sw) {
        var maxDepth = GetParam(cmd, "maxDepth", 5);
        var runtimeId = GetParam<string?>(cmd, "runtimeId");

        IUIAutomationElement? root = null;
        if (!string.IsNullOrEmpty(runtimeId)) {
            var node = _automation.FindElementByRuntimeId(runtimeId);
            root = node?.BackingElement;
        }

        var tree = _automation.GetElementTree(root, maxDepth);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, tree, sw.ElapsedMilliseconds));
    }

    private string HandleFind(Command cmd, System.Diagnostics.Stopwatch sw) {
        var criteria = BuildSearchCriteria(cmd);
        var results = _automation.FindElements(criteria);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { count = results.Count, elements = results }, sw.ElapsedMilliseconds));
    }

    private string HandleFindFirst(Command cmd, System.Diagnostics.Stopwatch sw) {
        var criteria = BuildSearchCriteria(cmd);
        var result = _automation.FindFirst(criteria);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, result, sw.ElapsedMilliseconds));
    }

    private string HandleClick(Command cmd, System.Diagnostics.Stopwatch sw) {
        var element = ResolveElement(cmd);
        _automation.Click(element);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { clicked = true }, sw.ElapsedMilliseconds));
    }

    private string HandleDoubleClick(Command cmd, System.Diagnostics.Stopwatch sw) {
        var element = ResolveElement(cmd);
        _automation.DoubleClick(element);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { doubleClicked = true }, sw.ElapsedMilliseconds));
    }

    private string HandleRightClick(Command cmd, System.Diagnostics.Stopwatch sw) {
        var element = ResolveElement(cmd);
        _automation.RightClick(element);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { rightClicked = true }, sw.ElapsedMilliseconds));
    }

    private string HandleSetValue(Command cmd, System.Diagnostics.Stopwatch sw) {
        var element = ResolveElement(cmd);
        var value = GetParam(cmd, "value", string.Empty);
        _automation.SetValue(element, value);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { setValue = value }, sw.ElapsedMilliseconds));
    }

    private string HandleInvoke(Command cmd, System.Diagnostics.Stopwatch sw) {
        var element = ResolveElement(cmd);
        _automation.Invoke(element);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { invoked = true }, sw.ElapsedMilliseconds));
    }

    private string HandleToggle(Command cmd, System.Diagnostics.Stopwatch sw) {
        var element = ResolveElement(cmd);
        _automation.Toggle(element);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { toggled = true }, sw.ElapsedMilliseconds));
    }

    private string HandleSelectItem(Command cmd, System.Diagnostics.Stopwatch sw) {
        var element = ResolveElement(cmd);
        var item = GetParam(cmd, "item", string.Empty);
        _automation.SelectItem(element, item);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { selected = item }, sw.ElapsedMilliseconds));
    }

    private string HandleExpand(Command cmd, System.Diagnostics.Stopwatch sw) {
        var element = ResolveElement(cmd);
        _automation.Expand(element);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { expanded = true }, sw.ElapsedMilliseconds));
    }

    private string HandleCollapse(Command cmd, System.Diagnostics.Stopwatch sw) {
        var element = ResolveElement(cmd);
        _automation.Collapse(element);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { collapsed = true }, sw.ElapsedMilliseconds));
    }

    private string HandleScrollIntoView(Command cmd, System.Diagnostics.Stopwatch sw) {
        var element = ResolveElement(cmd);
        _automation.ScrollIntoView(element);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { scrolledIntoView = true }, sw.ElapsedMilliseconds));
    }

    private string HandleGetProperty(Command cmd, System.Diagnostics.Stopwatch sw) {
        var element = ResolveElement(cmd);
        var propertyName = GetParam(cmd, "property", string.Empty);
        var value = _automation.GetPropertyValue(element, propertyName);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new Dictionary<string, object?> { [propertyName] = value }, sw.ElapsedMilliseconds));
    }

    private string HandleGetPatterns(Command cmd, System.Diagnostics.Stopwatch sw) {
        var element = ResolveElement(cmd);
        var patterns = _automation.GetSupportedPatterns(element);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { patterns }, sw.ElapsedMilliseconds));
    }

    private string HandleGetFocusedElement(Command cmd, System.Diagnostics.Stopwatch sw) {
        var element = _automation.GetFocusedElement();
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, element, sw.ElapsedMilliseconds));
    }

    private string HandleElementFromPoint(Command cmd, System.Diagnostics.Stopwatch sw) {
        var x = GetParam<double>(cmd, "x");
        var y = GetParam<double>(cmd, "y");
        var element = _automation.ElementFromPoint(x, y);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, element, sw.ElapsedMilliseconds));
    }

    private string HandleWaitForElement(Command cmd, System.Diagnostics.Stopwatch sw) {
        var criteria = BuildSearchCriteria(cmd);
        var timeoutMs = GetParam(cmd, "timeoutMs", 5000);
        var found = _automation.WaitForElement(criteria, timeoutMs);
        sw.Stop();
        return SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { found, timeoutMs }, sw.ElapsedMilliseconds));
    }

    private string HandleQuit(Command cmd, System.Diagnostics.Stopwatch sw) {
        sw.Stop();
        var response = SerializeResponse(CommandResponse.Ok(cmd.Id, cmd.Action, new { message = "Goodbye." }, sw.ElapsedMilliseconds));
        Environment.Exit(0);
        return response;
    }

    private static SearchCriteria BuildSearchCriteria(Command cmd) {
        return new SearchCriteria {
            Name = GetParam<string?>(cmd, "name"),
            AutomationId = GetParam<string?>(cmd, "automationId"),
            ClassName = GetParam<string?>(cmd, "className"),
            ControlType = GetParam<string?>(cmd, "controlType"),
            FrameworkId = GetParam<string?>(cmd, "frameworkId"),
            IsEnabled = GetParam<bool?>(cmd, "isEnabled"),
            ProcessId = GetParam<int?>(cmd, "processId"),
            MaxDepth = GetParam(cmd, "maxDepth", 5),
            SearchDescendants = GetParam(cmd, "searchDescendants", true)
        };
    }

    private static ElementNode ResolveElement(Command cmd) {
        var runtimeId = GetParam<string?>(cmd, "runtimeId");

        if (!string.IsNullOrEmpty(runtimeId)) {
            var svc = new AutomationService();
            var processId = GetParam<int?>(cmd, "processId");
            if (processId.HasValue)
                svc.AttachToProcess(processId.Value);

            var node = svc.FindElementByRuntimeId(runtimeId);
            if (node is not null) return node;
        }

        var criteria = BuildSearchCriteria(cmd);
        if (!criteria.IsEmpty) {
            var svc = new AutomationService();
            var matches = svc.FindElements(criteria);
            if (matches.Count > 0) return matches[0];
        }

        throw new InvalidOperationException("Cannot resolve element. Provide runtimeId or search criteria (name, automationId, className, controlType).");
    }

    private static T? GetParam<T>(Command cmd, string key) {
        if (cmd.Parameters.TryGetValue(key, out var val) && val is not null) {
            if (val is JsonElement jsonElement) {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }
            return (T)Convert.ChangeType(val, typeof(T));
        }
        return default;
    }

    private static T GetParam<T>(Command cmd, string key, T defaultValue) where T : notnull {
        if (cmd.Parameters.TryGetValue(key, out var val) && val is not null) {
            if (val is JsonElement jsonElement) {
                var deserialized = JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                if (deserialized is not null) return deserialized;
            }
            try { return (T)Convert.ChangeType(val, typeof(T))!; }
            catch { }
        }
        return defaultValue;
    }

    private string SerializeResponse(CommandResponse response) {
        return JsonSerializer.Serialize(response, _jsonOptions);
    }
}
