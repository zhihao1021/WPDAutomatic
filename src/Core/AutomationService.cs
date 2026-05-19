using System.Diagnostics;
using System.Runtime.InteropServices;
using Interop.UIAutomationClient;
using WPDAutomatic.Abstractions;
using WPDAutomatic.Models;

namespace WPDAutomatic.Core;

public sealed class AutomationService : IAutomationService, IDisposable {
    private readonly CUIAutomationClass _automation;
    private IUIAutomationElement? _rootElement;

    public AutomationService() {
        _automation = new CUIAutomationClass();
    }

    public IReadOnlyList<ProcessInfo> ListProcesses(string? filter = null) {
        var processes = Process.GetProcesses();
        return processes
            .Where(p => {
                try {
                    return !string.IsNullOrWhiteSpace(p.MainWindowTitle) && p.MainWindowHandle != IntPtr.Zero;
                }
                catch { return false; }
            })
            .Where(p => {
                if (filter is null) return true;
                try {
                    return p.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                           p.MainWindowTitle.Contains(filter, StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            })
            .OrderBy(p => p.ProcessName)
            .Select(p => new ProcessInfo {
                ProcessId = p.Id,
                ProcessName = p.ProcessName,
                MainWindowTitle = p.MainWindowTitle,
                MainWindowHandle = (long)p.MainWindowHandle,
                HasUi = true
            })
            .ToList();
    }

    public bool AttachToProcess(int processId) {
        try {
            var process = Process.GetProcessById(processId);
            if (process.MainWindowHandle == IntPtr.Zero)
                return false;

            _rootElement = _automation.ElementFromHandle(process.MainWindowHandle);
            return _rootElement is not null;
        }
        catch {
            return false;
        }
    }

    public bool AttachToWindow(IntPtr windowHandle) {
        try {
            _rootElement = _automation.ElementFromHandle(windowHandle);
            return _rootElement is not null;
        }
        catch {
            return false;
        }
    }

    public void Detach() {
        _rootElement = null;
    }

    public ElementNode? GetElementTree(IUIAutomationElement? root = null, int maxDepth = 5) {
        var element = root ?? _rootElement ?? _automation.GetRootElement();
        return BuildNode(element, maxDepth, 0);
    }

    public List<ElementNode> FindElements(SearchCriteria criteria, IUIAutomationElement? scope = null) {
        var scopeElement = scope ?? _rootElement ?? _automation.GetRootElement();
        var searchDescendants = criteria.SearchDescendants ?? true;
        var results = new List<ElementNode>();

        var condition = ConditionFactory.Build(_automation, criteria);
        var treeScope = searchDescendants ? TreeScope.TreeScope_Descendants : TreeScope.TreeScope_Children;

        try {
            var matches = scopeElement.FindAll(treeScope, condition);
            var count = matches.Length;
            for (int i = 0; i < count; i++) {
                var match = matches.GetElement(i);
                if (ConditionFactory.MatchesCriteria(match, criteria))
                    results.Add(BuildNode(match, 0, 0)!);
            }
        }
        catch { }

        return results;
    }

    public ElementNode? FindFirst(SearchCriteria criteria, IUIAutomationElement? scope = null) {
        var scopeElement = scope ?? _rootElement ?? _automation.GetRootElement();
        var searchDescendants = criteria.SearchDescendants ?? true;

        var condition = ConditionFactory.Build(_automation, criteria);
        var treeScope = searchDescendants ? TreeScope.TreeScope_Descendants : TreeScope.TreeScope_Children;

        try {
            var matches = scopeElement.FindAll(treeScope, condition);
            var count = matches.Length;
            for (int i = 0; i < count; i++) {
                var match = matches.GetElement(i);
                if (ConditionFactory.MatchesCriteria(match, criteria))
                    return BuildNode(match, 0, 0);
            }
        }
        catch { }

        return null;
    }

    public ElementNode? FindElementByRuntimeId(string runtimeId) {
        try {
            var parts = runtimeId.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var idArray = parts.Select(int.Parse).ToArray();

            var scope = _rootElement ?? _automation.GetRootElement();
            var matches = scope.FindAll(TreeScope.TreeScope_Descendants, _automation.CreateTrueCondition());
            var count = matches.Length;

            for (int i = 0; i < count; i++) {
                var el = matches.GetElement(i);
                try {
                    var rid = el.GetRuntimeId();
                    if (rid is not null && rid.Length == idArray.Length && rid.SequenceEqual(idArray))
                        return BuildNode(el, 0, 0);
                }
                catch { }
            }
        }
        catch { }

        return null;
    }

    public ElementNode? GetFocusedElement() {
        try {
            var focused = _automation.GetFocusedElement();
            return focused is not null ? BuildNode(focused, 0, 0) : null;
        }
        catch {
            return null;
        }
    }

    public ElementNode? ElementFromPoint(double x, double y) {
        try {
            var pt = new tagPOINT { x = (int)x, y = (int)y };
            var element = _automation.ElementFromPoint(pt);
            return element is not null ? BuildNode(element, 0, 0) : null;
        }
        catch {
            return null;
        }
    }

    public IUIAutomationElement? ResolveElement(ElementNode node) {
        if (node.BackingElement is not null)
            return node.BackingElement;

        if (!string.IsNullOrEmpty(node.RuntimeId))
            return FindBackingByRuntimeId(node.RuntimeId);

        return _rootElement;
    }

    public void Click(ElementNode element) {
        var el = ResolveElement(element) ?? throw new InvalidOperationException("Cannot resolve element");
        InvokeClick(el);
    }

    public void DoubleClick(ElementNode element) {
        var el = ResolveElement(element) ?? throw new InvalidOperationException("Cannot resolve element");
        if (TryGetClickablePoint(el, out var x, out var y)) {
            PerformMouseClick(x, y, true);
        }
        else {
            InvokeClick(el);
            Thread.Sleep(50);
            InvokeClick(el);
        }
    }

    public void RightClick(ElementNode element) {
        var el = ResolveElement(element) ?? throw new InvalidOperationException("Cannot resolve element");
        if (TryGetClickablePoint(el, out var x, out var y)) {
            PerformRightClick(x, y);
        }
        else {
            throw new InvalidOperationException("Cannot get clickable point for right-click");
        }
    }

    public void SetValue(ElementNode element, string value) {
        var el = ResolveElement(element) ?? throw new InvalidOperationException("Cannot resolve element");

        try {
            var vp = el.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId) as IUIAutomationValuePattern;
            if (vp is not null) {
                vp.SetValue(value);
                return;
            }
        }
        catch { }

        el.SetFocus();
        Thread.Sleep(30);
        SendKeys(value);
    }

    public void Invoke(ElementNode element) {
        var el = ResolveElement(element) ?? throw new InvalidOperationException("Cannot resolve element");

        try {
            var ip = el.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) as IUIAutomationInvokePattern;
            if (ip is not null) {
                ip.Invoke();
                return;
            }
        }
        catch { }

        throw new InvalidOperationException($"Element does not support InvokePattern. Supported: {string.Join(", ", GetSupportedPatterns(element))}");
    }

    public void SelectItem(ElementNode element, string item) {
        var el = ResolveElement(element) ?? throw new InvalidOperationException("Cannot resolve element");

        try {
            var sip = el.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId) as IUIAutomationSelectionItemPattern;
            if (sip is not null) {
                sip.Select();
                return;
            }
        }
        catch { }

        try {
            var ecp = el.GetCurrentPattern(UIA_PatternIds.UIA_ExpandCollapsePatternId) as IUIAutomationExpandCollapsePattern;
            if (ecp is not null) {
                ecp.Expand();
                Thread.Sleep(100);

                var matches = el.FindAll(TreeScope.TreeScope_Descendants, _automation.CreateTrueCondition());
                var count = matches.Length;
                for (int i = 0; i < count; i++) {
                    var child = matches.GetElement(i);
                    try {
                        if (string.Equals(child.CurrentName, item, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(child.CurrentAutomationId, item, StringComparison.OrdinalIgnoreCase)) {
                            var childSip = child.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId) as IUIAutomationSelectionItemPattern;
                            if (childSip is not null) {
                                childSip.Select();
                                return;
                            }
                            var childIp = child.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) as IUIAutomationInvokePattern;
                            if (childIp is not null) {
                                childIp.Invoke();
                                return;
                            }
                            InvokeClick(child);
                            return;
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        throw new InvalidOperationException($"Cannot select item '{item}'");
    }

    public void Toggle(ElementNode element) {
        var el = ResolveElement(element) ?? throw new InvalidOperationException("Cannot resolve element");

        try {
            var tp = el.GetCurrentPattern(UIA_PatternIds.UIA_TogglePatternId) as IUIAutomationTogglePattern;
            if (tp is not null) {
                tp.Toggle();
                return;
            }
        }
        catch { }

        throw new InvalidOperationException("Element does not support TogglePattern");
    }

    public void Expand(ElementNode element) {
        var el = ResolveElement(element) ?? throw new InvalidOperationException("Cannot resolve element");

        try {
            var ecp = el.GetCurrentPattern(UIA_PatternIds.UIA_ExpandCollapsePatternId) as IUIAutomationExpandCollapsePattern;
            if (ecp is not null) {
                ecp.Expand();
                return;
            }
        }
        catch { }

        throw new InvalidOperationException("Element does not support ExpandCollapsePattern");
    }

    public void Collapse(ElementNode element) {
        var el = ResolveElement(element) ?? throw new InvalidOperationException("Cannot resolve element");

        try {
            var ecp = el.GetCurrentPattern(UIA_PatternIds.UIA_ExpandCollapsePatternId) as IUIAutomationExpandCollapsePattern;
            if (ecp is not null) {
                ecp.Collapse();
                return;
            }
        }
        catch { }

        throw new InvalidOperationException("Element does not support ExpandCollapsePattern");
    }

    public void ScrollIntoView(ElementNode element) {
        var el = ResolveElement(element) ?? throw new InvalidOperationException("Cannot resolve element");

        try {
            var sip = el.GetCurrentPattern(UIA_PatternIds.UIA_ScrollItemPatternId) as IUIAutomationScrollItemPattern;
            if (sip is not null) {
                sip.ScrollIntoView();
                return;
            }
        }
        catch { }

        el.SetFocus();
    }

    public List<string> GetSupportedPatterns(ElementNode element) {
        var el = ResolveElement(element) ?? throw new InvalidOperationException("Cannot resolve element");
        return GetAvailablePatterns(el);
    }

    public object? GetPropertyValue(ElementNode element, string propertyName) {
        var el = ResolveElement(element) ?? throw new InvalidOperationException("Cannot resolve element");

        try {
            return propertyName.ToUpperInvariant() switch {
                "NAME" => el.CurrentName ?? string.Empty,
                "AUTOMATIONID" => el.CurrentAutomationId ?? string.Empty,
                "CLASsNAME" => el.CurrentClassName ?? string.Empty,
                "CONTROLTYPE" => ControlTypeIdToName(el.CurrentControlType),
                "ISENABLED" => el.CurrentIsEnabled != 0,
                "ISOFFSCREEN" => el.CurrentIsOffscreen != 0,
                "BOUNDINGRECTANGLE" => RectToString(el.CurrentBoundingRectangle),
                "HELPTEXT" => el.CurrentHelpText ?? string.Empty,
                "FRAMEWORKID" => el.CurrentFrameworkId ?? string.Empty,
                "PROCESSID" => el.CurrentProcessId,
                "ISPASSWORD" => el.CurrentIsPassword != 0,
                "ACCELERATORKEY" => el.CurrentAcceleratorKey ?? string.Empty,
                "ACCESSKEY" => el.CurrentAccessKey ?? string.Empty,
                "ITEMSTATUS" => el.CurrentItemStatus ?? string.Empty,
                "ORIENTATION" => el.CurrentOrientation.ToString(),
                "NATIVEWINDOWHANDLE" => el.CurrentNativeWindowHandle,
                "ISKEYBOARDFOCUSABLE" => el.CurrentIsKeyboardFocusable != 0,
                "HASKEYBOARDFOCUS" => el.CurrentHasKeyboardFocus != 0,
                "ITEMTYPE" => el.CurrentItemType ?? string.Empty,
                "ARIAROLE" => el.CurrentAriaRole ?? string.Empty,
                "PROVIDERDESCRIPTION" => el.CurrentProviderDescription ?? string.Empty,
                _ => el.GetCurrentPropertyValue(PropertyNameToId(propertyName))
            };
        }
        catch (Exception ex) {
            return $"Error reading property: {ex.Message}";
        }
    }

    public bool WaitForElement(SearchCriteria criteria, int timeoutMs, IUIAutomationElement? scope = null) {
        var scopeElement = scope ?? _rootElement ?? _automation.GetRootElement();
        var searchDescendants = criteria.SearchDescendants ?? true;
        var deadline = Environment.TickCount64 + (uint)timeoutMs;

        while (Environment.TickCount64 < deadline) {
            var condition = ConditionFactory.Build(_automation, criteria);
            var treeScope = searchDescendants ? TreeScope.TreeScope_Descendants : TreeScope.TreeScope_Children;

            try {
                var matches = scopeElement.FindAll(treeScope, condition);
                var count = matches.Length;
                for (int i = 0; i < count; i++) {
                    var match = matches.GetElement(i);
                    if (ConditionFactory.MatchesCriteria(match, criteria))
                        return true;
                }
            }
            catch { }

            Thread.Sleep(200);
        }

        return false;
    }

    public void Dispose() {
        Detach();
    }

    private ElementNode? BuildNode(IUIAutomationElement element, int maxDepth, int currentDepth) {
        if (element is null) return null;

        try {
            int[]? runtimeIdArray = null;
            try { runtimeIdArray = element.GetRuntimeId(); } catch { }

            var node = new ElementNode {
                Name = SafeGet(() => element.CurrentName) ?? string.Empty,
                AutomationId = SafeGet(() => element.CurrentAutomationId) ?? string.Empty,
                ControlType = SafeGet(() => ControlTypeIdToName(element.CurrentControlType)) ?? string.Empty,
                ClassName = SafeGet(() => element.CurrentClassName) ?? string.Empty,
                IsEnabled = SafeGet(() => element.CurrentIsEnabled != 0),
                IsOffscreen = SafeGet(() => element.CurrentIsOffscreen != 0),
                BoundingRectangle = SafeGet<BoundingRectangle?>(() => {
                    var r = element.CurrentBoundingRectangle;
                    return new BoundingRectangle { X = r.left, Y = r.top, Width = r.right - r.left, Height = r.bottom - r.top };
                }),
                HelpText = SafeGet(() => element.CurrentHelpText) ?? string.Empty,
                Value = SafeGet<string?>(() => {
                    try {
                        var vp = element.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId) as IUIAutomationValuePattern;
                        return vp?.CurrentValue ?? null;
                    }
                    catch { return null; }
                }),
                IsPassword = SafeGet(() => element.CurrentIsPassword != 0),
                FrameworkId = SafeGet(() => element.CurrentFrameworkId) ?? string.Empty,
                ProcessId = SafeGet(() => element.CurrentProcessId),
                RuntimeId = runtimeIdArray is not null ? string.Join(",", runtimeIdArray) : string.Empty,
                Patterns = GetAvailablePatternsStatic(element),
                BackingElement = element
            };

            if (currentDepth < maxDepth) {
                try {
                    var children = element.FindAll(TreeScope.TreeScope_Children, _automation.CreateTrueCondition());
                    var childCount = children.Length;
                    for (int i = 0; i < childCount; i++) {
                        var child = children.GetElement(i);
                        var childNode = BuildNode(child, maxDepth, currentDepth + 1);
                        if (childNode is not null)
                            node.Children.Add(childNode);
                    }
                }
                catch { }
            }

            return node;
        }
        catch {
            return null;
        }
    }

    private static List<string> GetAvailablePatternsStatic(IUIAutomationElement element) {
        return GetAvailablePatternsImpl(element);
    }

    private List<string> GetAvailablePatterns(IUIAutomationElement element) {
        return GetAvailablePatternsImpl(element);
    }

    private static List<string> GetAvailablePatternsImpl(IUIAutomationElement element) {
        var patterns = new List<string>();
        var knownPatterns = new (int PatternId, string Name)[]
        {
            (UIA_PatternIds.UIA_InvokePatternId, "Invoke"),
            (UIA_PatternIds.UIA_ValuePatternId, "Value"),
            (UIA_PatternIds.UIA_TogglePatternId, "Toggle"),
            (UIA_PatternIds.UIA_ExpandCollapsePatternId, "ExpandCollapse"),
            (UIA_PatternIds.UIA_SelectionPatternId, "Selection"),
            (UIA_PatternIds.UIA_SelectionItemPatternId, "SelectionItem"),
            (UIA_PatternIds.UIA_ScrollPatternId, "Scroll"),
            (UIA_PatternIds.UIA_ScrollItemPatternId, "ScrollItem"),
            (UIA_PatternIds.UIA_RangeValuePatternId, "RangeValue"),
            (UIA_PatternIds.UIA_TransformPatternId, "Transform"),
            (UIA_PatternIds.UIA_WindowPatternId, "Window"),
            (UIA_PatternIds.UIA_TextPatternId, "Text"),
            (UIA_PatternIds.UIA_GridPatternId, "Grid"),
            (UIA_PatternIds.UIA_GridItemPatternId, "GridItem"),
            (UIA_PatternIds.UIA_TablePatternId, "Table"),
            (UIA_PatternIds.UIA_TableItemPatternId, "TableItem"),
            (UIA_PatternIds.UIA_DockPatternId, "Dock"),
            (UIA_PatternIds.UIA_MultipleViewPatternId, "MultipleView"),
        };

        foreach (var (patternId, name) in knownPatterns) {
            try {
                var p = element.GetCurrentPattern(patternId);
                if (p is not null)
                    patterns.Add(name);
            }
            catch { }
        }

        return patterns;
    }

    private void InvokeClick(IUIAutomationElement element) {
        try {
            var ip = element.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) as IUIAutomationInvokePattern;
            if (ip is not null) {
                ip.Invoke();
                return;
            }
        }
        catch { }

        try {
            var tp = element.GetCurrentPattern(UIA_PatternIds.UIA_TogglePatternId) as IUIAutomationTogglePattern;
            if (tp is not null) {
                tp.Toggle();
                return;
            }
        }
        catch { }

        try {
            var sip = element.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId) as IUIAutomationSelectionItemPattern;
            if (sip is not null) {
                sip.Select();
                return;
            }
        }
        catch { }

        if (TryGetClickablePoint(element, out var cx, out var cy)) {
            PerformMouseClick(cx, cy, false);
            return;
        }

        throw new InvalidOperationException($"Cannot click element. Supported patterns: {string.Join(", ", GetAvailablePatterns(element))}");
    }

    private bool TryGetClickablePoint(IUIAutomationElement element, out int x, out int y) {
        try {
            var pt = new tagPOINT();
            var result = element.GetClickablePoint(out pt);
            if (result == 0) {
                x = pt.x;
                y = pt.y;
                return true;
            }
        }
        catch { }

        try {
            var rect = element.CurrentBoundingRectangle;
            x = rect.left + (rect.right - rect.left) / 2;
            y = rect.top + (rect.bottom - rect.top) / 2;
            return rect.right > rect.left && rect.bottom > rect.top;
        }
        catch { }

        x = 0;
        y = 0;
        return false;
    }

    private static void PerformMouseClick(int x, int y, bool doubleClick) {
        var originalPos = System.Windows.Forms.Cursor.Position;
        try {
            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
            Thread.Sleep(30);

            NativeMethods.SendMouseInput(NativeMethods.MOUSEEVENTF_LEFTDOWN, 0, 0);
            Thread.Sleep(10);
            NativeMethods.SendMouseInput(NativeMethods.MOUSEEVENTF_LEFTUP, 0, 0);

            if (doubleClick) {
                Thread.Sleep(50);
                NativeMethods.SendMouseInput(NativeMethods.MOUSEEVENTF_LEFTDOWN, 0, 0);
                Thread.Sleep(10);
                NativeMethods.SendMouseInput(NativeMethods.MOUSEEVENTF_LEFTUP, 0, 0);
            }
        }
        finally {
            System.Windows.Forms.Cursor.Position = originalPos;
        }
    }

    private static void PerformRightClick(int x, int y) {
        var originalPos = System.Windows.Forms.Cursor.Position;
        try {
            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
            Thread.Sleep(30);

            NativeMethods.SendMouseInput(NativeMethods.MOUSEEVENTF_RIGHTDOWN, 0, 0);
            Thread.Sleep(10);
            NativeMethods.SendMouseInput(NativeMethods.MOUSEEVENTF_RIGHTUP, 0, 0);
        }
        finally {
            System.Windows.Forms.Cursor.Position = originalPos;
        }
    }

    private static void SendKeys(string keys) {
        System.Windows.Forms.SendKeys.SendWait(keys);
    }

    private IUIAutomationElement? FindBackingByRuntimeId(string runtimeId) {
        try {
            var parts = runtimeId.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var idArray = parts.Select(int.Parse).ToArray();

            var scope = _rootElement ?? _automation.GetRootElement();
            var matches = scope.FindAll(TreeScope.TreeScope_Descendants, _automation.CreateTrueCondition());
            var count = matches.Length;

            for (int i = 0; i < count; i++) {
                var el = matches.GetElement(i);
                try {
                    var rid = el.GetRuntimeId();
                    if (rid is not null && rid.Length == idArray.Length && rid.SequenceEqual(idArray))
                        return el;
                }
                catch { }
            }
        }
        catch { }

        return null;
    }

    private static string ControlTypeIdToName(int controlTypeId) {
        return controlTypeId switch {
            UIA_ControlTypeIds.UIA_ButtonControlTypeId => "Button",
            UIA_ControlTypeIds.UIA_CalendarControlTypeId => "Calendar",
            UIA_ControlTypeIds.UIA_CheckBoxControlTypeId => "CheckBox",
            UIA_ControlTypeIds.UIA_ComboBoxControlTypeId => "ComboBox",
            UIA_ControlTypeIds.UIA_EditControlTypeId => "Edit",
            UIA_ControlTypeIds.UIA_HyperlinkControlTypeId => "Hyperlink",
            UIA_ControlTypeIds.UIA_ImageControlTypeId => "Image",
            UIA_ControlTypeIds.UIA_ListItemControlTypeId => "ListItem",
            UIA_ControlTypeIds.UIA_ListControlTypeId => "List",
            UIA_ControlTypeIds.UIA_MenuControlTypeId => "Menu",
            UIA_ControlTypeIds.UIA_MenuBarControlTypeId => "MenuBar",
            UIA_ControlTypeIds.UIA_MenuItemControlTypeId => "MenuItem",
            UIA_ControlTypeIds.UIA_ProgressBarControlTypeId => "ProgressBar",
            UIA_ControlTypeIds.UIA_RadioButtonControlTypeId => "RadioButton",
            UIA_ControlTypeIds.UIA_ScrollBarControlTypeId => "ScrollBar",
            UIA_ControlTypeIds.UIA_SliderControlTypeId => "Slider",
            UIA_ControlTypeIds.UIA_SpinnerControlTypeId => "Spinner",
            UIA_ControlTypeIds.UIA_StatusBarControlTypeId => "StatusBar",
            UIA_ControlTypeIds.UIA_TabControlTypeId => "Tab",
            UIA_ControlTypeIds.UIA_TabItemControlTypeId => "TabItem",
            UIA_ControlTypeIds.UIA_TextControlTypeId => "Text",
            UIA_ControlTypeIds.UIA_ToolBarControlTypeId => "ToolBar",
            UIA_ControlTypeIds.UIA_ToolTipControlTypeId => "ToolTip",
            UIA_ControlTypeIds.UIA_TreeControlTypeId => "Tree",
            UIA_ControlTypeIds.UIA_TreeItemControlTypeId => "TreeItem",
            UIA_ControlTypeIds.UIA_WindowControlTypeId => "Window",
            UIA_ControlTypeIds.UIA_DataGridControlTypeId => "DataGrid",
            UIA_ControlTypeIds.UIA_SplitButtonControlTypeId => "SplitButton",
            UIA_ControlTypeIds.UIA_DocumentControlTypeId => "Document",
            UIA_ControlTypeIds.UIA_GroupControlTypeId => "Group",
            UIA_ControlTypeIds.UIA_HeaderControlTypeId => "Header",
            UIA_ControlTypeIds.UIA_HeaderItemControlTypeId => "HeaderItem",
            UIA_ControlTypeIds.UIA_TableControlTypeId => "Table",
            UIA_ControlTypeIds.UIA_TitleBarControlTypeId => "TitleBar",
            UIA_ControlTypeIds.UIA_SeparatorControlTypeId => "Separator",
            UIA_ControlTypeIds.UIA_ThumbControlTypeId => "Thumb",
            UIA_ControlTypeIds.UIA_CustomControlTypeId => "Custom",
            UIA_ControlTypeIds.UIA_DataItemControlTypeId => "DataItem",
            UIA_ControlTypeIds.UIA_PaneControlTypeId => "Pane",
            UIA_ControlTypeIds.UIA_SemanticZoomControlTypeId => "SemanticZoom",
            UIA_ControlTypeIds.UIA_AppBarControlTypeId => "AppBar",
            _ => $"Unknown({controlTypeId})"
        };
    }

    private static int PropertyNameToId(string propertyName) {
        return propertyName.ToUpperInvariant() switch {
            "RUNTIMEID" => UIA_PropertyIds.UIA_RuntimeIdPropertyId,
            "BOUNDINGRECTANGLE" => UIA_PropertyIds.UIA_BoundingRectanglePropertyId,
            "PROCESSID" => UIA_PropertyIds.UIA_ProcessIdPropertyId,
            "CONTROLTYPE" => UIA_PropertyIds.UIA_ControlTypePropertyId,
            "LOCALIZEDCONTROLTYPE" => UIA_PropertyIds.UIA_LocalizedControlTypePropertyId,
            "NAME" => UIA_PropertyIds.UIA_NamePropertyId,
            "ACCELERATORKEY" => UIA_PropertyIds.UIA_AcceleratorKeyPropertyId,
            "ACCESSKEY" => UIA_PropertyIds.UIA_AccessKeyPropertyId,
            "HASKEYBOARDFOCUS" => UIA_PropertyIds.UIA_HasKeyboardFocusPropertyId,
            "ISKEYBOARDFOCUSABLE" => UIA_PropertyIds.UIA_IsKeyboardFocusablePropertyId,
            "ISENABLED" => UIA_PropertyIds.UIA_IsEnabledPropertyId,
            "AUTOMATIONID" => UIA_PropertyIds.UIA_AutomationIdPropertyId,
            "CLASsNAME" => UIA_PropertyIds.UIA_ClassNamePropertyId,
            "HELPTEXT" => UIA_PropertyIds.UIA_HelpTextPropertyId,
            "CLICKABLEPOINT" => UIA_PropertyIds.UIA_ClickablePointPropertyId,
            "CULTURE" => UIA_PropertyIds.UIA_CulturePropertyId,
            "ISCONTROLELEMENT" => UIA_PropertyIds.UIA_IsControlElementPropertyId,
            "ISCONTENTELEMENT" => UIA_PropertyIds.UIA_IsContentElementPropertyId,
            "ISPASSWORD" => UIA_PropertyIds.UIA_IsPasswordPropertyId,
            "NATIVEWINDOWHANDLE" => UIA_PropertyIds.UIA_NativeWindowHandlePropertyId,
            "ITEMTYPE" => UIA_PropertyIds.UIA_ItemTypePropertyId,
            "ISOFFSCREEN" => UIA_PropertyIds.UIA_IsOffscreenPropertyId,
            "ORIENTATION" => UIA_PropertyIds.UIA_OrientationPropertyId,
            "FRAMEWORKID" => UIA_PropertyIds.UIA_FrameworkIdPropertyId,
            "ISREQUIREDFORFORM" => UIA_PropertyIds.UIA_IsRequiredForFormPropertyId,
            "ITEMSTATUS" => UIA_PropertyIds.UIA_ItemStatusPropertyId,
            _ => UIA_PropertyIds.UIA_NamePropertyId
        };
    }

    private static string RectToString(tagRECT rect) {
        return $"{{left={rect.left}, top={rect.top}, right={rect.right}, bottom={rect.bottom}}}";
    }

    private static T SafeGet<T>(Func<T> getter, T defaultValue = default!) {
        try { return getter(); }
        catch { return defaultValue; }
    }
}
