# WPDAutomatic - Agent Guide

WPDAutomatic 是一個基於 Windows UI Automation (UIA) 的命令列工具，透過 stdin/stdout 的 JSON 協定讓 AI Agent 可以操控 Windows 應用程式。

## 使用方式

```bash
# 啟動 (可選：指定目標處理程序名稱過濾)
WPDAutomatic.exe
```

啟動後，工具等待來自 stdin 的 JSON 命令，並將 JSON 回應輸出到 stdout。

## JSON 通訊協定

### 請求格式

```json
{
  "id": "req-001",
  "action": "action_name",
  "parameters": {
    "key": "value"
  }
}
```

### 回應格式

```json
{
  "id": "req-001",
  "success": true,
  "action": "action_name",
  "data": { ... },
  "error": null,
  "elapsedMs": 15
}
```

## 支援的 Actions (命令)

### 1. `list_processes` - 列出執行中的視窗處理程序

```json
{"id":"1","action":"list_processes","parameters":{"filter":"notepad"}}
```

回傳具有主視窗的處理程序清單，`filter` 選填，用於過濾名稱。

---

### 2. `attach` - 附加到處理程序

```json
{"id":"2","action":"attach","parameters":{"processId":12345}}
```

---

### 3. `attach_window` - 附加到指定視窗 Handle

```json
{"id":"3","action":"attach_window","parameters":{"handle":987654}}
```

---

### 4. `detach` - 分離目前附加的目標

```json
{"id":"4","action":"detach","parameters":{}}
```

---

### 5. `get_tree` - 取得 UI 元素樹

```json
{"id":"5","action":"get_tree","parameters":{"maxDepth":3}}
```

可指定 `runtimeId` 以取得子樹，`maxDepth` 控制深度（預設 5）。

回傳的 ElementNode 結構：

```json
{
  "name": "OK",
  "automationId": "btnOK",
  "controlType": "Button",
  "className": "Button",
  "isEnabled": true,
  "isOffscreen": false,
  "boundingRectangle": {"x":100,"y":200,"width":75,"height":23},
  "helpText": "",
  "value": null,
  "isPassword": false,
  "frameworkId": "WinForm",
  "processId": 12345,
  "runtimeId": "42,1234567",
  "patterns": ["Invoke"],
  "children": []
}
```

---

### 6. `find` - 搜尋元素 (多個)

```json
{
  "id":"6",
  "action":"find",
  "parameters":{
    "name":"OK",
    "controlType":"Button",
    "searchDescendants":true,
    "maxDepth":5
  }
}
```

搜尋條件 (SearchCriteria)：

| 參數 | 類型 | 說明 |
|------|------|------|
| `name` | string | 元素名稱 |
| `automationId` | string | Automation ID |
| `className` | string | 類別名稱 |
| `controlType` | string | 控制項類型 (見下方類型表) |
| `frameworkId` | string | 框架 ID (WinForm, WPF, Win32) |
| `isEnabled` | bool | 是否啟用 |
| `processId` | int | 處理程序 ID |
| `searchDescendants` | bool | 是否搜尋子元素 (預設 true) |
| `maxDepth` | int | 最大搜尋深度 (預設 5) |

---

### 7. `find_first` - 搜尋元素 (第一個)

與 `find` 參數相同，但只回傳第一個符合的元素。

```json
{"id":"7","action":"find_first","parameters":{"name":"OK","controlType":"Button"}}
```

---

### 8. `click` - 點擊元素

```json
{"id":"8","action":"click","parameters":{"runtimeId":"42,1234567"}}
```

也可以透過搜尋條件直接定位：

```json
{"id":"8","action":"click","parameters":{"name":"OK","controlType":"Button"}}
```

---

### 9. `double_click` - 雙擊元素

```json
{"id":"9","action":"double_click","parameters":{"runtimeId":"42,1234567"}}
```

---

### 10. `right_click` - 右鍵點擊元素

```json
{"id":"10","action":"right_click","parameters":{"runtimeId":"42,1234567"}}
```

---

### 11. `set_value` - 設定文字值

```json
{"id":"11","action":"set_value","parameters":{"runtimeId":"42,1234567","value":"Hello"}}
```

---

### 12. `invoke` - 觸發 Invoke 模式

對按鈕等元素直接觸發 InvokePattern。

```json
{"id":"12","action":"invoke","parameters":{"runtimeId":"42,1234567"}}
```

---

### 13. `toggle` - 切換開關

```json
{"id":"13","action":"toggle","parameters":{"runtimeId":"42,1234567"}}
```

---

### 14. `select_item` - 選取清單項目

```json
{"id":"14","action":"select_item","parameters":{"runtimeId":"42,1234567","item":"Option A"}}
```

---

### 15. `expand` - 展開元素

```json
{"id":"15","action":"expand","parameters":{"runtimeId":"42,1234567"}}
```

---

### 16. `collapse` - 折疊元素

```json
{"id":"16","action":"collapse","parameters":{"runtimeId":"42,1234567"}}
```

---

### 17. `scroll_into_view` - 滾動到可見

```json
{"id":"17","action":"scroll_into_view","parameters":{"runtimeId":"42,1234567"}}
```

---

### 18. `get_property` - 取得屬性值

```json
{"id":"18","action":"get_property","parameters":{"runtimeId":"42,1234567","property":"Name"}}
```

支援的屬性名稱 (不區分大小寫)：
`Name`, `AutomationId`, `ClassName`, `ControlType`, `IsEnabled`, `IsOffscreen`, `BoundingRectangle`, `HelpText`, `FrameworkId`, `ProcessId`, `IsPassword`, `AcceleratorKey`, `AccessKey`, `ItemStatus`, `Orientation`, `NativeWindowHandle`, `IsKeyboardFocusable`, `HasKeyboardFocus`, `ItemType`, `AriaRole`, `ProviderDescription`

---

### 19. `get_patterns` - 取得支援的 Pattern 清單

```json
{"id":"19","action":"get_patterns","parameters":{"runtimeId":"42,1234567"}}
```

---

### 20. `get_focused_element` - 取得目前焦點元素

```json
{"id":"20","action":"get_focused_element","parameters":{}}
```

---

### 21. `element_from_point` - 從座標取得元素

```json
{"id":"21","action":"element_from_point","parameters":{"x":500,"y":300}}
```

---

### 22. `wait_for_element` - 等待元素出現

```json
{"id":"22","action":"wait_for_element","parameters":{"name":"Ready","timeoutMs":10000}}
```

搜尋條件與 `find` 相同，額外參數 `timeoutMs`（預設 5000ms）。

---

### 23. `quit` - 退出

```json
{"id":"23","action":"quit","parameters":{}}
```

---

## 支援的 ControlType (控制項類型)

| 值 | 說明 |
|---|------|
| `Button` | 按鈕 |
| `Edit` | 文字輸入框 |
| `CheckBox` | 核取方塊 |
| `RadioButton` | 選項按鈕 |
| `ComboBox` | 下拉方塊 |
| `List` | 清單 |
| `ListItem` | 清單項目 |
| `Tree` | 樹狀結構 |
| `TreeItem` | 樹狀結構項目 |
| `Tab` | 分頁容器 |
| `TabItem` | 分頁 |
| `Menu` | 選單 |
| `MenuBar` | 選單列 |
| `MenuItem` | 選單項目 |
| `Window` | 視窗 |
| `Pane` | 面板 |
| `Group` | 群組 |
| `Text` | 文字 |
| `Hyperlink` | 超連結 |
| `Image` | 圖片 |
| `Table` | 表格 |
| `DataGrid` | 資料表格 |
| `Document` | 文件 |
| `ToolBar` | 工具列 |
| `ToolTip` | 提示文字 |
| `ProgressBar` | 進度條 |
| `ScrollBar` | 捲動條 |
| `Slider` | 滑桿 |
| `Spinner` | 微調器 |
| `StatusBar` | 狀態列 |
| `TitleBar` | 標題列 |
| `SplitButton` | 分割按鈕 |
| `Header` | 標頭 |
| `HeaderItem` | 標頭項目 |
| `Separator` | 分隔線 |
| `Thumb` | 捲動方塊 |
| `Calendar` | 月曆 |
| `Custom` | 自訂 |
| `DataItem` | 資料項目 |
| `AppBar` | 應用程式列 |
| `SemanticZoom` | 語意縮放 |

---

## 典型工作流程

```
1. list_processes          → 尋找目標應用程式的 PID
2. attach                  → 附加到目標處理程序
3. get_tree                → 探索 UI 結構和元素
4. find / find_first       → 定位要操作的特定元素
5. click / set_value / ... → 執行操作
6. get_tree                → 檢查操作後的狀態
7. detach / quit           → 清理
```

## 範例對話

```
→ {"id":"1","action":"list_processes","parameters":{"filter":"notepad"}}
← {"id":"1","success":true,"action":"list_processes","data":[{"processId":8420,"processName":"notepad","mainWindowTitle":"Untitled - Notepad",...}],"elapsedMs":5}

→ {"id":"2","action":"attach","parameters":{"processId":8420}}
← {"id":"2","success":true,"action":"attach","data":{"attached":true},"elapsedMs":3}

→ {"id":"3","action":"find_first","parameters":{"controlType":"Edit"}}
← {"id":"3","success":true,"action":"find_first","data":{"name":"","controlType":"Edit","runtimeId":"42,2758860",...},"elapsedMs":12}

→ {"id":"4","action":"set_value","parameters":{"runtimeId":"42,2758860","value":"Hello from AI!"}}
← {"id":"4","success":true,"action":"set_value","data":{"setValue":"Hello from AI!"},"elapsedMs":18}
```

## 專案結構

```
WPDAutomatic/
└── src/
    ├── Models/
    │   ├── ElementNode.cs       # UI 元素節點資料模型
    │   ├── Command.cs           # 輸入命令模型
    │   ├── CommandResponse.cs   # 回應模型
    │   ├── ProcessInfo.cs       # 處理程序資訊
    │   └── SearchCriteria.cs    # 搜尋條件
    ├── Abstractions/
    │   └── IAutomationService.cs # 自動化服務介面
    ├── Core/
    │   ├── AutomationService.cs  # UIA 自動化實作
    │   ├── ConditionFactory.cs   # UIA 條件建構器
    │   └── NativeMethods.cs      # Win32 P/Invoke
    ├── Commands/
    │   └── CommandRouter.cs      # JSON 命令路由器
    └── Program.cs               # 入口點
```
