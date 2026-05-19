using Interop.UIAutomationClient;
using WPDAutomatic.Models;

namespace WPDAutomatic.Abstractions;

public interface IAutomationService
{
    IReadOnlyList<ProcessInfo> ListProcesses(string? filter = null);
    bool AttachToProcess(int processId);
    bool AttachToWindow(IntPtr windowHandle);
    void Detach();

    ElementNode? GetElementTree(IUIAutomationElement? root = null, int maxDepth = 5);
    List<ElementNode> FindElements(SearchCriteria criteria, IUIAutomationElement? scope = null);
    ElementNode? FindFirst(SearchCriteria criteria, IUIAutomationElement? scope = null);
    ElementNode? FindElementByRuntimeId(string runtimeId);

    ElementNode? GetFocusedElement();
    ElementNode? ElementFromPoint(double x, double y);

    IUIAutomationElement? ResolveElement(ElementNode node);

    void Click(ElementNode element);
    void DoubleClick(ElementNode element);
    void RightClick(ElementNode element);
    void SetValue(ElementNode element, string value);
    void Invoke(ElementNode element);
    void SelectItem(ElementNode element, string item);
    void Toggle(ElementNode element);
    void Expand(ElementNode element);
    void Collapse(ElementNode element);
    void ScrollIntoView(ElementNode element);

    List<string> GetSupportedPatterns(ElementNode element);
    object? GetPropertyValue(ElementNode element, string propertyName);
    bool WaitForElement(SearchCriteria criteria, int timeoutMs, IUIAutomationElement? scope = null);
}
