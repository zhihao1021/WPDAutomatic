# WPDAutomatic

> [!WARNING]
> **此專案由 LLM 生成，尚未經過完整的人工審查與測試。**
>
> 請在使用前仔細檢查程式碼邏輯、安全性與正確性。本專案涉及 Windows UI Automation 操作，不當使用可能導致資料遺失或系統不穩定。**請在非生產環境中充分測試後再部署使用。**

---

WPDAutomatic 是一個 **Windows UI Automation** 工具，可以讓 AI Agent（如 OpenCode、Claude Desktop 等支援 MCP 的客戶端）直接操控 Windows 桌面應用程式（WinForm / WPF / Win32）。
> ~~終於不用再一份一份改作業啦~~

核心基於 `.NET 10` 與 `Interop.UIAutomationClient` COM API 實作，並以 **MCP (Model Context Protocol)** 作為主要通訊協定，同時保留獨立的 stdin/stdout JSON 模式。

---

## 目錄

- [Demo](#demo)
- [快速開始](#快速開始)
- [運作模式](#運作模式)
  - [MCP 模式（推薦）](#mcp-模式推薦)
  - [獨立 JSON 模式](#獨立-json-模式)
- [MCP 工具列表](#mcp-工具列表)
- [專案架構](#專案架構)
- [開發指南](#開發指南)
- [相依套件](#相依套件)

---

## Demo
[https://youtu.be/JXxh_VGvys4](https://youtu.be/JXxh_VGvys4)

## 快速開始

### 環境需求

- Windows 10 x64 或以上
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [OpenCode](https://opencode.ai)（或其他支援 MCP 的 AI 客戶端）

### 建置

```powershell
git clone https://github.com/zhihao1021/WPDAutomatic
cd WPDAutomatic
dotnet build -c Release
```

建置成功後，執行檔位於 `bin/Release/net10.0-windows/WPDAutomatic.exe`。

---

## 運作模式

### MCP 模式（推薦）

以 MCP JSON-RPC 2.0 協定提供 20 個 UI 自動化工具，可由 OpenCode 直接呼叫。

#### 設定 OpenCode

在專案根目錄的 `opencode.json`（或 `opencode.jsonc`）中加入：

```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "uia": {
      "type": "local",
      "enabled": true,
      "command": [
        "C:\\path\\to\\WPDAutomatic\\bin\\Release\\net10.0-windows\\WPDAutomatic.exe",
        "--mcp"
      ],
      "timeout": 30000
    }
  }
}
```

> [!IMPORTANT]
> 請將 `command` 中的路徑替換為實際的 `WPDAutomatic.exe` 絕對路徑。
> OpenCode 啟動時會自動 spawn 此行程，並在關閉時終止。

#### 設定完成後的使用方式

在 OpenCode 對話中直接描述你要做的事，Agent 會自動呼叫 UIA 工具：

```
幫我開啟記事本，輸入 "Hello from MCP"，然後儲存到桌面。
```

Agent 的典型操作流程：

```
uia_list_processes → 找到 notepad PID
uia_attach        → 附加到 notepad 行程
uia_get_tree      → 查看 UI 結構
uia_find          → 定位編輯區域
uia_type          → 輸入文字
uia_click         → 點擊選單按鈕
uia_click         → 點擊儲存選項
```

> [!NOTE]
> 也可以在提示詞中明確指定使用 `uia` 工具：
> `use the uia tools to click the OK button in the dialog`

---

### 獨立 JSON 模式

不帶 `--mcp` 參數啟動時，WPDAutomatic 以自訂 JSON 協定透過 stdin/stdout 通訊，適合嵌入其他自動化流程。

```powershell
.\WPDAutomatic.exe
```

詳細協定請參閱 [AGENT_GUIDE.md](./AGENT_GUIDE.md)。

---

## MCP 工具列表

共 20 個工具，以 `uia_` 為前綴：

### 處理程序管理

| 工具 | 說明 | 必要參數 |
|------|------|----------|
| `uia_list_processes` | 列出有可見視窗的處理程序 | `filter`（選填） |
| `uia_attach` | 附加 UIA 到目標處理程序 | `processId` |

### UI 探索

| 工具 | 說明 | 必要參數 |
|------|------|----------|
| `uia_get_tree` | 取得完整 UI 元素樹，含屬性與 patterns | `maxDepth`（選填，預設 4） |
| `uia_find` | 搜尋第一個匹配的 UI 元素 | `name` / `controlType` 等 |
| `uia_find_all` | 搜尋所有匹配的 UI 元素 | `name` / `controlType` 等 |
| `uia_get_focused` | 取得目前焦點元素 | 無 |
| `uia_get_property` | 讀取元素屬性值 | `runtimeId`, `property` |
| `uia_get_patterns` | 取得元素支援的 Patterns | `runtimeId` |
| `uia_wait_for_element` | 等待元素出現（支援超時） | `name`/`controlType`, `timeoutMs` |

### 互動操作

| 工具 | 說明 | 必要參數 |
|------|------|----------|
| `uia_click` | 點擊元素 | `runtimeId`（優先）或搜尋條件 |
| `uia_double_click` | 雙擊元素 | `runtimeId` |
| `uia_right_click` | 右鍵點擊元素 | `runtimeId` |
| `uia_type` | 在輸入框輸入文字 | `runtimeId`, `value` |
| `uia_invoke` | 觸發 InvokePattern（按鈕） | `runtimeId` |
| `uia_select` | 從下拉/清單選取項目 | `runtimeId`, `item` |
| `uia_toggle` | 切換 CheckBox 開關 | `runtimeId` |
| `uia_expand` | 展開樹狀節點或下拉 | `runtimeId` |
| `uia_collapse` | 折疊樹狀節點或下拉 | `runtimeId` |
| `uia_scroll_into_view` | 捲動元素到可見區域 | `runtimeId` |

### 搜尋條件參數

`uia_find`、`uia_find_all`、`uia_click` 等工具支援以下搜尋欄位：

| 參數 | 類型 | 說明 |
|------|------|------|
| `name` | string | 元素名稱 (如 `OK`, `Cancel`) |
| `automationId` | string | Automation ID (如 `btnSubmit`) |
| `className` | string | 類別名稱 (如 `Button`, `Edit`) |
| `controlType` | string | 控制項類型 (見下方) |
| `isEnabled` | boolean | 是否啟用 |
| `searchDescendants` | boolean | 是否遞迴搜尋子元素 (預設 true) |
| `maxDepth` | integer | 最大搜尋深度 (預設 5) |

### 支援的 ControlType

`Button`, `Edit`, `CheckBox`, `RadioButton`, `ComboBox`, `List`, `ListItem`, `Tree`, `TreeItem`, `Tab`, `TabItem`, `Menu`, `MenuBar`, `MenuItem`, `Window`, `Pane`, `Group`, `Text`, `Hyperlink`, `Image`, `Table`, `DataGrid`, `Document`, `ToolBar`, `ToolTip`, `ProgressBar`, `ScrollBar`, `Slider`, `Spinner`, `StatusBar`, `TitleBar`, `SplitButton`, `Header`, `HeaderItem`, `Separator`, `Thumb`, `Calendar`, `Custom`

---

## 專案架構

```
WPDAutomatic/
├── .gitignore
├── AGENT_GUIDE.md                  # 獨立 JSON 模式協定手冊
├── opencode.json                   # OpenCode MCP 設定範例
├── README.md                       # 本檔案
├── WPDAutomatic.csproj             # .NET 10 專案檔
├── mcps/
│   └── README.md                   # MCP 模式安裝指引
└── src/
    ├── Program.cs                  # 進入點 (--mcp / 預設)
    │
    ├── Models/                     # 資料模型層
    │   ├── Command.cs              # 輸入命令模型
    │   ├── CommandResponse.cs      # 回應模型
    │   ├── ElementNode.cs          # UI 元素樹節點
    │   ├── ProcessInfo.cs          # 處理程序資訊
    │   └── SearchCriteria.cs       # 元素搜尋條件
    │
    ├── Abstractions/               # 介面層
    │   └── IAutomationService.cs   # UIA 自動化服務介面
    │
    ├── Core/                       # 核心實作層
    │   ├── AutomationService.cs    # UIA COM 自動化引擎 (~720 行)
    │   ├── ConditionFactory.cs     # UIA 條件建構器
    │   └── NativeMethods.cs        # Win32 P/Invoke (滑鼠/鍵盤)
    │
    ├── Commands/                   # 獨立 JSON 模式
    │   └── CommandRouter.cs        # JSON 命令路由器 (23 個 action)
    │
    └── Mcp/                        # MCP 模式
        ├── McpServer.cs            # MCP JSON-RPC 2.0 伺服器
        └── UiaToolDefinitions.cs   # 20 個 MCP 工具定義與執行邏輯
```

### 架構設計原則

```
┌─────────────────────────────────────────┐
│  Program.cs (entry point)               │
│    ├── --mcp → McpServer                │
│    └── (default) → CommandRouter        │
├─────────────────────────────────────────┤
│  McpServer ────┐  ┌──── CommandRouter   │
│  (JSON-RPC)    │  │    (自訂 JSON)       │
│                │  │                      │
│           ┌────┴──┴────┐                 │
│           │ IAutomationService │         │
│           └────────┬───────────┘         │
│                    │                     │
│           ┌────────▼───────────┐         │
│           │ AutomationService  │         │
│           │ (COM UIA Engine)   │         │
│           └────────┬───────────┘         │
│                    │                     │
│     ┌──────────────┼──────────────┐      │
│     │              │              │      │
│  Interop.     Condition      Native      │
│  UIAutomation Factory       Methods      │
│  Client       (搜尋條件)    (滑鼠/鍵盤)   │
└─────────────────────────────────────────┘
```

- **Models** — 純資料物件，無外部相依
- **Abstractions** — 定義 `IAutomationService` 介面，隔離實作細節
- **Core** — 所有 UIA 邏輯集中於 `AutomationService`，內聚性高
- **Commands** — 僅依賴 Core，處理獨立 JSON 協定
- **Mcp** — 僅依賴 Core，處理 MCP JSON-RPC 協定

兩種模式共享同一個 `AutomationService` 實例，確保行為一致。

---

## 開發指南

### 新增 MCP 工具

在 `src/Mcp/UiaToolDefinitions.cs` 的 `GetTools()` 方法中，使用 `CreateTool()` 輔助方法新增工具：

```csharp
CreateTool(
    "uia_my_new_tool",                    // 工具名稱 (必須 uia_ 前綴)
    "Description of what this tool does", // 工具說明
    new                                    // JSON Schema 定義
    {
        type = "object",
        properties = new
        {
            paramA = new
            {
                type = "string",
                description = "Description of paramA"
            }
        },
        required = new[] { "paramA" }
    },
    (args, svc) =>                         // 執行邏輯
    {
        var value = GetString(args, "paramA");
        // 呼叫 svc (AutomationService) 的方法
        return $"Result: {value}";
    }
)
```

### 新增獨立 JSON Action

在 `src/Commands/CommandRouter.cs` 的 `ExecuteCommand()` 方法中新增 case，並實作對應的 `Handle*` 方法。

### 修改 UIA 核心邏輯

所有 UIA 操作集中在 `src/Core/AutomationService.cs`。該類別實作 `IAutomationService` 介面，透過 `Interop.UIAutomationClient` COM API 與 Windows UIA 子系統通訊。

關鍵方法：

| 方法 | 說明 |
|------|------|
| `ListProcesses()` | 列舉有視窗的處理程序 |
| `AttachToProcess()` | 附加到目標行程 |
| `GetElementTree()` | 遍歷 UI 元素樹 |
| `FindElements()` | 依條件搜尋元素 |
| `Click()` / `SetValue()` / `Invoke()` 等 | 元素互動 |
| `WaitForElement()` | 輪詢等待元素出現 |

### 建置與測試

```powershell
# Debug 建置
dotnet build

# Release 建置
dotnet build -c Release

# 獨立模式測試 (JSON 協定)
echo '{"id":"1","action":"list_processes","parameters":{}}' | .\bin\Debug\net10.0-windows\WPDAutomatic.exe

# MCP 模式測試 (JSON-RPC)
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | .\bin\Debug\net10.0-windows\WPDAutomatic.exe --mcp
```

### 程式碼風格

- C# 12 語法，檔案範圍命名空間 (`namespace X;`)
- `Nullable` 啟用，所有參考類型預設不可為 null
- `ImplicitUsings` 啟用
- 使用 `Interop.UIAutomationClient` COM API（非 Managed UIA）
- 所有 COM 操作包裹 try-catch，避免 Crash
- P/Invoke 呼叫集中在 `NativeMethods.cs`

---

## 相依套件

| 套件 | 版本 | 說明 |
|------|------|------|
| [Interop.UIAutomationClient](https://www.nuget.org/packages/Interop.UIAutomationClient) | 10.19041.0 | Windows UIA COM Interop |
| `System.Windows.Forms` | (內建) | 用於 `Cursor.Position` 與 `SendKeys` |

專案目標框架：`net10.0-windows`

---

## 已知限制

1. **僅支援 Windows** — UIA 是 Windows 特有的 API
2. **需要目標應用程式可見** — 某些背景執行的應用程式可能不暴露 UIA 元素
3. **COM 效能** — 大型 UI 樹的遍歷可能較慢，建議使用 `maxDepth` 控制深度
4. **部分自訂控制項** — 非標準繪製的控制項可能不支援 UIA patterns
5. **管理員權限** — 操控以管理員身分執行的應用程式時，WPDAutomatic 也需以管理員身分執行
