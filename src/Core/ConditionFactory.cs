using Interop.UIAutomationClient;
using WPDAutomatic.Models;

namespace WPDAutomatic.Core;

internal static class ConditionFactory {
    public static IUIAutomationCondition Build(CUIAutomationClass automation, SearchCriteria criteria) {
        if (criteria.IsEmpty)
            return automation.CreateTrueCondition();

        var conditions = new List<IUIAutomationCondition>();

        if (criteria.Name is not null) {
            conditions.Add(automation.CreatePropertyCondition(
                UIA_PropertyIds.UIA_NamePropertyId, criteria.Name));
        }

        if (criteria.AutomationId is not null) {
            conditions.Add(automation.CreatePropertyCondition(
                UIA_PropertyIds.UIA_AutomationIdPropertyId, criteria.AutomationId));
        }

        if (criteria.ClassName is not null) {
            conditions.Add(automation.CreatePropertyCondition(
                UIA_PropertyIds.UIA_ClassNamePropertyId, criteria.ClassName));
        }

        if (criteria.ControlType is not null && TryParseControlTypeId(criteria.ControlType, out var ctId)) {
            conditions.Add(automation.CreatePropertyCondition(
                UIA_PropertyIds.UIA_ControlTypePropertyId, ctId));
        }

        if (criteria.FrameworkId is not null) {
            conditions.Add(automation.CreatePropertyCondition(
                UIA_PropertyIds.UIA_FrameworkIdPropertyId, criteria.FrameworkId));
        }

        if (criteria.IsEnabled.HasValue) {
            conditions.Add(automation.CreatePropertyCondition(
                UIA_PropertyIds.UIA_IsEnabledPropertyId, criteria.IsEnabled.Value ? 1 : 0));
        }

        if (criteria.ProcessId.HasValue) {
            conditions.Add(automation.CreatePropertyCondition(
                UIA_PropertyIds.UIA_ProcessIdPropertyId, criteria.ProcessId.Value));
        }

        return conditions.Count switch {
            0 => automation.CreateTrueCondition(),
            1 => conditions[0],
            _ => automation.CreateAndConditionFromArray([.. conditions])
        };
    }

    public static bool MatchesCriteria(IUIAutomationElement element, SearchCriteria criteria) {
        if (criteria.Name is not null) {
            try {
                if (!string.Equals(element.CurrentName, criteria.Name, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            catch { return false; }
        }

        if (criteria.AutomationId is not null) {
            try {
                if (!string.Equals(element.CurrentAutomationId, criteria.AutomationId, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            catch { return false; }
        }

        if (criteria.ClassName is not null) {
            try {
                if (!string.Equals(element.CurrentClassName, criteria.ClassName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            catch { return false; }
        }

        if (criteria.ControlType is not null) {
            try {
                var ctId = ParseControlTypeId(criteria.ControlType);
                if (!ctId.HasValue || element.CurrentControlType != ctId.Value)
                    return false;
            }
            catch { return false; }
        }

        if (criteria.FrameworkId is not null) {
            try {
                if (!string.Equals(element.CurrentFrameworkId, criteria.FrameworkId, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            catch { return false; }
        }

        if (criteria.IsEnabled.HasValue) {
            try {
                if ((element.CurrentIsEnabled != 0) != criteria.IsEnabled.Value)
                    return false;
            }
            catch { return false; }
        }

        if (criteria.ProcessId.HasValue) {
            try {
                if (element.CurrentProcessId != criteria.ProcessId.Value)
                    return false;
            }
            catch { return false; }
        }

        return true;
    }

    public static int? ParseControlTypeId(string name) {
        return name.ToUpperInvariant() switch {
            "BUTTON" => UIA_ControlTypeIds.UIA_ButtonControlTypeId,
            "CALENDAR" => UIA_ControlTypeIds.UIA_CalendarControlTypeId,
            "CHECKBOX" => UIA_ControlTypeIds.UIA_CheckBoxControlTypeId,
            "COMBOBOX" => UIA_ControlTypeIds.UIA_ComboBoxControlTypeId,
            "EDIT" => UIA_ControlTypeIds.UIA_EditControlTypeId,
            "HYPERLINK" => UIA_ControlTypeIds.UIA_HyperlinkControlTypeId,
            "IMAGE" => UIA_ControlTypeIds.UIA_ImageControlTypeId,
            "LISTITEM" => UIA_ControlTypeIds.UIA_ListItemControlTypeId,
            "LIST" => UIA_ControlTypeIds.UIA_ListControlTypeId,
            "MENU" => UIA_ControlTypeIds.UIA_MenuControlTypeId,
            "MENUBAR" => UIA_ControlTypeIds.UIA_MenuBarControlTypeId,
            "MENUITEM" => UIA_ControlTypeIds.UIA_MenuItemControlTypeId,
            "PANE" => UIA_ControlTypeIds.UIA_PaneControlTypeId,
            "PROGRESSBAR" => UIA_ControlTypeIds.UIA_ProgressBarControlTypeId,
            "RADIOBUTTON" => UIA_ControlTypeIds.UIA_RadioButtonControlTypeId,
            "SCROLLBAR" => UIA_ControlTypeIds.UIA_ScrollBarControlTypeId,
            "SLIDER" => UIA_ControlTypeIds.UIA_SliderControlTypeId,
            "SPINNER" => UIA_ControlTypeIds.UIA_SpinnerControlTypeId,
            "STATUSBAR" => UIA_ControlTypeIds.UIA_StatusBarControlTypeId,
            "TAB" => UIA_ControlTypeIds.UIA_TabControlTypeId,
            "TABITEM" => UIA_ControlTypeIds.UIA_TabItemControlTypeId,
            "TEXT" => UIA_ControlTypeIds.UIA_TextControlTypeId,
            "TOOLBAR" => UIA_ControlTypeIds.UIA_ToolBarControlTypeId,
            "TOOLTIP" => UIA_ControlTypeIds.UIA_ToolTipControlTypeId,
            "TREE" => UIA_ControlTypeIds.UIA_TreeControlTypeId,
            "TREEITEM" => UIA_ControlTypeIds.UIA_TreeItemControlTypeId,
            "WINDOW" => UIA_ControlTypeIds.UIA_WindowControlTypeId,
            "DATAGRID" => UIA_ControlTypeIds.UIA_DataGridControlTypeId,
            "SPLITBUTTON" => UIA_ControlTypeIds.UIA_SplitButtonControlTypeId,
            "DOCUMENT" => UIA_ControlTypeIds.UIA_DocumentControlTypeId,
            "GROUP" => UIA_ControlTypeIds.UIA_GroupControlTypeId,
            "HEADER" => UIA_ControlTypeIds.UIA_HeaderControlTypeId,
            "HEADERITEM" => UIA_ControlTypeIds.UIA_HeaderItemControlTypeId,
            "TABLE" => UIA_ControlTypeIds.UIA_TableControlTypeId,
            "TITLEBAR" => UIA_ControlTypeIds.UIA_TitleBarControlTypeId,
            "SEPARATOR" => UIA_ControlTypeIds.UIA_SeparatorControlTypeId,
            "THUMB" => UIA_ControlTypeIds.UIA_ThumbControlTypeId,
            "CUSTOM" => UIA_ControlTypeIds.UIA_CustomControlTypeId,
            "DATAITEM" => UIA_ControlTypeIds.UIA_DataItemControlTypeId,
            "APPBAR" => UIA_ControlTypeIds.UIA_AppBarControlTypeId,
            "SEMANTICZOOM" => UIA_ControlTypeIds.UIA_SemanticZoomControlTypeId,
            _ => null
        };
    }

    private static bool TryParseControlTypeId(string name, out int ctId) {
        var parsed = ParseControlTypeId(name);
        if (parsed.HasValue) {
            ctId = parsed.Value;
            return true;
        }
        ctId = 0;
        return false;
    }
}
