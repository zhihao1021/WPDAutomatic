# WPDAutomatic - MCP Server for OpenCode

透過 MCP (Model Context Protocol) 讓 OpenCode 可以操控 Windows 應用程式。

## 安裝與設定

### 1. 建置 WPDAutomatic

```powershell
cd WPDAutomatic
dotnet build -c Release
```

### 2. 設定 OpenCode

在專案的 `opencode.json` (或 `opencode.jsonc`) 中新增 MCP server 設定：

```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "uia": {
      "type": "local",
      "command": [
        "C:\\path\\to\\WPDAutomatic\\bin\\Release\\net10.0-windows\\WPDAutomatic.exe",
        "--mcp"
      ],
      "enabled": true,
      "timeout": 30000
    }
  }
}
```

> **注意**: 請將路徑替換為你實際的 WPDAutomatic.exe 路徑。
> `timeout` 設為 30000ms (30秒)，因為某些 UIA 操作（如 `uia_get_tree`）可能需要較長時間。

## 提供的 MCP 工具 (20 個)

| 工具名稱 | 功能 | 必要參數 |
|---------|------|---------|
| `uia_list_processes` | 列出所有有視窗的處理程序 | `filter` (選填) |
| `uia_attach` | 附加到目標處理程序 | `processId` |
| `uia_get_tree` | 取得完整 UI 元素樹 | `maxDepth` (選填) |
| `uia_find` | 搜尋第一個符合的元素 | `name` / `controlType` 等 |
| `uia_find_all` | 搜尋所有符合的元素 | `name` / `controlType` 等 |
| `uia_click` | 點擊元素 | `runtimeId` |
| `uia_double_click` | 雙擊元素 | `runtimeId` |
| `uia_right_click` | 右鍵點擊元素 | `runtimeId` |
| `uia_type` | 輸入文字 | `runtimeId`, `value` |
| `uia_invoke` | 觸發按鈕 (InvokePattern) | `runtimeId` |
| `uia_select` | 從下拉選單選取 | `runtimeId`, `item` |
| `uia_toggle` | 切換 CheckBox | `runtimeId` |
| `uia_expand` | 展開節點/下拉 | `runtimeId` |
| `uia_collapse` | 折疊節點/下拉 | `runtimeId` |
| `uia_get_property` | 讀取元素屬性 | `runtimeId`, `property` |
| `uia_get_focused` | 取得焦點元素 | - |
| `uia_get_patterns` | 取得支援的 Patterns | `runtimeId` |
| `uia_wait_for_element` | 等待元素出現 | `name`/`controlType`, `timeoutMs` |
| `uia_scroll_into_view` | 捲動到可見 | `runtimeId` |

## 使用範例

在 OpenCode 對話中：

```
User: 幫我在記事本輸入 "Hello from MCP" 然後存檔

OpenCode 會自動呼叫:
  uia_list_processes → 找到 notepad PID
  uia_attach → 附加到 notepad
  uia_get_tree → 查看 UI 結構
  uia_type → 在編輯區輸入文字
  uia_click → 點擊 "檔案" 選單
  uia_click → 點擊 "儲存"
```

## 協定說明

本專案實作 MCP JSON-RPC 2.0 協定，透過 stdin/stdout 通訊：

- `initialize` → 協商協定版本與能力
- `tools/list` → 列出可用工具
- `tools/call` → 執行工具

與 `extension/` 中的 TypeScript 版本不同，MCP 版本直接由 OpenCode 管理生命週期，不需額外安裝 `@opencode-ai/plugin` 套件。MCP server 以子行程方式執行，OpenCode 自動處理啟動與終止。

## 與 extension 版本的比較

| 特性 | extension (TypeScript) | mcps (MCP) |
|------|----------------------|------------|
| 實作語言 | TypeScript | C# (內建) |
| 啟動方式 | bun install + 工具定義 | opencode.json 設定 |
| 生命週期 | 每次呼叫 spawn 新行程 | OpenCode 管理持久行程 |
| 效能 | 每次冷啟動 (~100ms) | 熱呼叫 (~1ms) |
| 相依性 | 需 @opencode-ai/plugin | 無外部相依 |
| 工具數量 | 16 | 20 |

建議使用 MCP 版本以獲得更好的效能與更簡單的設定。
