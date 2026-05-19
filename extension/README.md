# WPDAutomatic - OpenCode Extension

讓 OpenCode AI Agent 可以直接操控 Windows 應用程式 (WinForm / WPF / Win32)。

## 安裝方式

### 1. 複製到你的 OpenCode 專案

將整個 `extension/` 目錄內容複製到你的專案根目錄，使結構如下：

```
your-project/
├── .opencode/
│   ├── package.json        ← OpenCode 擴充套件相依
│   └── tools/
│       └── uia.ts          ← UIA 工具定義
├── src/
├── opencode.json           (或 opencode.jsonc)
└── ...
```

或者建立符號連結：

```bash
# PowerShell (系統管理員)
New-Item -ItemType Junction -Path "your-project\.opencode" -Target "C:\path\to\WPDAutomatic\extension\.opencode"
```

### 2. 安裝相依套件

```bash
cd your-project
bun install
```

### 3. 建置 WPDAutomatic

在 WPDAutomatic 專案目錄中：

```bash
dotnet build -c Release
```

### 4. 設定環境變數 (選填)

如果 WPDAutomatic.exe 不在預設路徑 (`extension/bin/Debug/net10.0-windows/`)，設定環境變數：

```powershell
$env:WPDAUTOMATIC_PATH = "C:\path\to\WPDAutomatic\bin\Release\net10.0-windows\WPDAutomatic.exe"
```

或在 `opencode.json` 中設定：

```json
{
  "env": {
    "WPDAUTOMATIC_PATH": "C:\\path\\to\\WPDAutomatic.exe"
  }
}
```

## 提供給 OpenCode 的工具

安裝後，OpenCode 會獲得以下 16 個工具：

| 工具名稱 | 功能 | 必要參數 |
|---------|------|---------|
| `uia_list` | 列出所有有視窗的處理程序 | `filter` (選填) |
| `uia_attach` | 附加到目標處理程序 | `processId` |
| `uia_tree` | 取得完整 UI 元素樹 | `maxDepth` (選填) |
| `uia_find` | 搜尋特定 UI 元素 | `name` / `automationId` / `controlType` |
| `uia_click` | 點擊 UI 元素 | `runtimeId` |
| `uia_double_click` | 雙擊 UI 元素 | `runtimeId` |
| `uia_type` | 在輸入框輸入文字 | `runtimeId`, `value` |
| `uia_invoke` | 觸發按鈕 (InvokePattern) | `runtimeId` |
| `uia_select` | 從下拉選取項目 | `runtimeId`, `item` |
| `uia_toggle` | 切換核取方塊 | `runtimeId` |
| `uia_expand` | 展開樹狀節點/下拉 | `runtimeId` |
| `uia_collapse` | 折疊樹狀節點 | `runtimeId` |
| `uia_get_property` | 讀取元素屬性 | `runtimeId`, `property` |
| `uia_get_focused` | 取得焦點元素 | - |
| `uia_wait` | 等待元素出現 | `name`, `timeoutMs` (選填) |
| `uia_scroll_into_view` | 捲動元素到可見 | `runtimeId` |
| `uia_get_patterns` | 取得支援的 Patterns | `runtimeId` |

## 使用範例

在 OpenCode 對話中，Agent 會自動呼叫這些工具。典型對話流程：

```
User: 幫我在記事本中輸入 "Hello World" 並儲存

Agent 呼叫:
1. uia_list { filter: "notepad" }          → 找到記事本 PID
2. uia_attach { processId: 8420 }          → 附加到記事本
3. uia_tree { maxDepth: 3 }                → 查看 UI 結構
4. uia_type { runtimeId: "42,2758860", value: "Hello World" }
                                            → 輸入文字
5. uia_click { runtimeId: "42,2758900" }   → 點擊 "檔案" 選單
6. uia_click { name: "儲存", controlType: "MenuItem" }
                                            → 點擊儲存
```

## 檔案結構

```
extension/
├── .opencode/
│   ├── package.json         # npm 相依 (含 @opencode-ai/plugin)
│   └── tools/
│       └── uia.ts           # 16 個 UIA 工具定義 (TypeScript)
└── README.md               # 本檔案
```
