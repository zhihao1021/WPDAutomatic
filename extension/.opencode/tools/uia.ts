import { tool } from "@opencode-ai/plugin"
import { spawn } from "child_process"
import { join } from "path"

const __dirname = new URL(".", import.meta.url).pathname
const EXE_PATH = join(__dirname, "..", "..", "..", "bin", "Debug", "net10.0-windows", "WPDAutomatic.exe")

function resolveExe(): string {
  if (process.env.WPDAUTOMATIC_PATH) return process.env.WPDAUTOMATIC_PATH
  return EXE_PATH
}

async function sendCommand(action: string, params: Record<string, unknown>): Promise<unknown> {
  const exePath = resolveExe()
  const id = crypto.randomUUID()

  return new Promise((resolve, reject) => {
    const child = spawn(exePath, [], {
      stdio: ["pipe", "pipe", "pipe"],
      cwd: process.cwd(),
    })

    let stdout = ""
    let stderr = ""

    child.stdout?.on("data", (data: Buffer) => {
      stdout += data.toString()
      const lines = stdout.split("\n")
      for (const line of lines) {
        try {
          const response = JSON.parse(line.trim())
          if (response && response.id === id) {
            child.kill()
            resolve(response)
            return
          }
        } catch {
          // incomplete JSON, continue accumulating
          continue
        }
      }
    })

    child.stderr?.on("data", (data: Buffer) => {
      stderr += data.toString()
    })

    child.on("close", (code) => {
      if (stderr && !stdout) {
        reject(new Error(`WPDAutomatic exited with code ${code}: ${stderr.trim()}`))
        return
      }
      try {
        const response = JSON.parse(stdout.trim())
        resolve(response)
      } catch {
        reject(new Error(`WPDAutomatic failed (code ${code}): ${stdout.trim() || stderr.trim()}`))
      }
    })

    child.on("error", (err) => {
      reject(new Error(`Failed to start WPDAutomatic: ${err.message}. Make sure the .exe is built: dotnet build`))
    })

    const request = JSON.stringify({ id, action, parameters: params })
    child.stdin?.write(request + "\n")
    child.stdin?.end()
  })
}

function resolveElementParam(params: Record<string, unknown>) {
  if (params.runtimeId) {
    return { runtimeId: params.runtimeId }
  }
  return {
    name: params.name,
    automationId: params.automationId,
    className: params.className,
    controlType: params.controlType,
    frameworkId: params.frameworkId,
    isEnabled: params.isEnabled,
    processId: params.processId,
    searchDescendants: params.searchDescendants ?? true,
    maxDepth: params.maxDepth ?? 5,
  }
}

export const uia_list = tool({
  description:
    "List running Windows processes that have a visible window (UI). Use this to find the target application to automate.",
  args: {
    filter: tool.schema.string().optional().describe("Optional filter to match process name or window title"),
  },
  async execute(args) {
    const result: any = await sendCommand("list_processes", { filter: args.filter ?? null })
    if (!result.success) return JSON.stringify({ error: result.error })
    const processes: any[] = result.data
    if (processes.length === 0) return "No processes with visible windows found."
    return processes
      .map(
        (p) =>
          `PID: ${p.processId} | ${p.processName} - "${p.mainWindowTitle}" (HWND: ${p.mainWindowHandle})`,
      )
      .join("\n")
  },
})

export const uia_attach = tool({
  description:
    "Attach WPDAutomatic to a target process by its PID. Must be called before other UIA operations.",
  args: {
    processId: tool.schema.number().describe("The process ID (PID) of the target application"),
  },
  async execute(args) {
    const result: any = await sendCommand("attach", { processId: args.processId })
    if (!result.success) return JSON.stringify({ error: result.error })
    return `Successfully attached to process ${args.processId}.`
  },
})

export const uia_tree = tool({
  description:
    "Get the full UI Automation element tree of the attached application. Returns the complete hierarchy of UI elements with their properties, patterns, and runtimeIds. Use this to discover the UI structure before interacting with elements.",
  args: {
    maxDepth: tool.schema.number().optional().default(4).describe("Maximum depth of the tree (default: 4)"),
    runtimeId: tool.schema
      .string()
      .optional()
      .describe("Optional runtime ID of a specific element to get its subtree"),
  },
  async execute(args) {
    const result: any = await sendCommand("get_tree", {
      maxDepth: args.maxDepth ?? 4,
      runtimeId: args.runtimeId ?? null,
    })
    if (!result.success) return JSON.stringify({ error: result.error })

    function formatNode(node: any, depth: number): string {
      const indent = "  ".repeat(depth)
      const patterns = node.patterns?.length ? ` [${node.patterns.join(", ")}]` : ""
      let line = `${indent}${node.controlType || "?"}`
      if (node.name) line += ` "${node.name}"`
      if (node.automationId) line += ` #${node.automationId}`
      line += ` rid:${node.runtimeId}`
      if (!node.isEnabled) line += " (DISABLED)"
      if (node.isOffscreen) line += " (OFFSCREEN)"
      line += patterns
      const children = node.children
        ?.map((c: any) => formatNode(c, depth + 1))
        .join("\n")
      return children ? line + "\n" + children : line
    }

    return formatNode(result.data, 0)
  },
})

export const uia_find = tool({
  description:
    "Find UI Automation elements matching search criteria. Returns the first match with full details including runtimeId for subsequent operations.",
  args: {
    name: tool.schema.string().optional().describe("Element name (e.g., 'OK', 'Submit')"),
    automationId: tool.schema
      .string()
      .optional()
      .describe("Automation ID (e.g., 'btnSubmit', 'txtName')"),
    className: tool.schema.string().optional().describe("Class name (e.g., 'Button', 'Edit')"),
    controlType: tool.schema
      .string()
      .optional()
      .describe("Control type: Button, Edit, ComboBox, CheckBox, ListItem, MenuItem, TabItem, TreeItem, Window, etc."),
    isEnabled: tool.schema.boolean().optional().describe("Filter by enabled state"),
    searchDescendants: tool.schema
      .boolean()
      .optional()
      .default(true)
      .describe("Search descendants (default: true)"),
    maxDepth: tool.schema.number().optional().default(5).describe("Max search depth"),
  },
  async execute(args) {
    const criteria = resolveElementParam(args)
    const result: any = await sendCommand("find_first", criteria)
    if (!result.success) return JSON.stringify({ error: result.error })
    if (!result.data) return "No matching element found."
    const el = result.data
    return JSON.stringify(
      {
        name: el.name,
        automationId: el.automationId,
        controlType: el.controlType,
        className: el.className,
        isEnabled: el.isEnabled,
        runtimeId: el.runtimeId,
        patterns: el.patterns,
        boundingRect: el.boundingRectangle,
        value: el.value,
        processId: el.processId,
        frameworkId: el.frameworkId,
      },
      null,
      2,
    )
  },
})

export const uia_click = tool({
  description:
    "Click a UI element by its runtimeId (obtained from uia_tree or uia_find). Falls back to search criteria if runtimeId not provided.",
  args: {
    runtimeId: tool.schema.string().optional().describe("Runtime ID of the element to click"),
    name: tool.schema.string().optional().describe("Fallback: element name to search and click"),
    controlType: tool.schema.string().optional().describe("Fallback: control type filter"),
  },
  async execute(args) {
    const params: Record<string, unknown> = {}
    if (args.runtimeId) {
      params.runtimeId = args.runtimeId
    } else {
      Object.assign(params, resolveElementParam(args))
    }
    const result: any = await sendCommand("click", params)
    if (!result.success) return JSON.stringify({ error: result.error })
    return `Clicked element successfully.`
  },
})

export const uia_double_click = tool({
  description: "Double-click a UI element by its runtimeId.",
  args: {
    runtimeId: tool.schema.string().describe("Runtime ID of the element to double-click"),
  },
  async execute(args) {
    const result: any = await sendCommand("double_click", { runtimeId: args.runtimeId })
    if (!result.success) return JSON.stringify({ error: result.error })
    return `Double-clicked element successfully.`
  },
})

export const uia_type = tool({
  description: "Type text into a text input field (Edit control) identified by its runtimeId.",
  args: {
    runtimeId: tool.schema.string().describe("Runtime ID of the text input element"),
    value: tool.schema.string().describe("Text to type into the input field"),
  },
  async execute(args) {
    const result: any = await sendCommand("set_value", {
      runtimeId: args.runtimeId,
      value: args.value,
    })
    if (!result.success) return JSON.stringify({ error: result.error })
    return `Text typed successfully: "${args.value}"`
  },
})

export const uia_invoke = tool({
  description:
    "Invoke a UI element (typically a Button) by its runtimeId. Uses the InvokePattern which is the preferred way to activate buttons.",
  args: {
    runtimeId: tool.schema.string().describe("Runtime ID of the element to invoke"),
  },
  async execute(args) {
    const result: any = await sendCommand("invoke", { runtimeId: args.runtimeId })
    if (!result.success) return JSON.stringify({ error: result.error })
    return `Element invoked successfully.`
  },
})

export const uia_select = tool({
  description:
    "Select an item from a ComboBox, ListBox, or similar selection control. Provide the parent element runtimeId and the item name to select.",
  args: {
    runtimeId: tool.schema.string().describe("Runtime ID of the ComboBox/List control"),
    item: tool.schema.string().describe("Name of the item to select"),
  },
  async execute(args) {
    const result: any = await sendCommand("select_item", {
      runtimeId: args.runtimeId,
      item: args.item,
    })
    if (!result.success) return JSON.stringify({ error: result.error })
    return `Selected "${args.item}" successfully.`
  },
})

export const uia_toggle = tool({
  description: "Toggle a CheckBox or other toggleable element by its runtimeId.",
  args: {
    runtimeId: tool.schema.string().describe("Runtime ID of the toggle element"),
  },
  async execute(args) {
    const result: any = await sendCommand("toggle", { runtimeId: args.runtimeId })
    if (!result.success) return JSON.stringify({ error: result.error })
    return `Element toggled successfully.`
  },
})

export const uia_expand = tool({
  description: "Expand a tree node, combo box, or other expandable element by its runtimeId.",
  args: {
    runtimeId: tool.schema.string().describe("Runtime ID of the element to expand"),
  },
  async execute(args) {
    const result: any = await sendCommand("expand", { runtimeId: args.runtimeId })
    if (!result.success) return JSON.stringify({ error: result.error })
    return `Element expanded successfully.`
  },
})

export const uia_collapse = tool({
  description: "Collapse a tree node or other collapsible element by its runtimeId.",
  args: {
    runtimeId: tool.schema.string().describe("Runtime ID of the element to collapse"),
  },
  async execute(args) {
    const result: any = await sendCommand("collapse", { runtimeId: args.runtimeId })
    if (!result.success) return JSON.stringify({ error: result.error })
    return `Element collapsed successfully.`
  },
})

export const uia_get_property = tool({
  description: "Read a specific property value from a UI element identified by its runtimeId.",
  args: {
    runtimeId: tool.schema.string().describe("Runtime ID of the element"),
    property: tool.schema
      .string()
      .describe(
        "Property name: Name, AutomationId, ClassName, ControlType, IsEnabled, IsOffscreen, BoundingRectangle, HelpText, FrameworkId, ProcessId, IsPassword, Value, AcceleratorKey, AccessKey, ItemStatus, Orientation, NativeWindowHandle, IsKeyboardFocusable, HasKeyboardFocus",
      ),
  },
  async execute(args) {
    const result: any = await sendCommand("get_property", {
      runtimeId: args.runtimeId,
      property: args.property,
    })
    if (!result.success) return JSON.stringify({ error: result.error })
    return JSON.stringify(result.data, null, 2)
  },
})

export const uia_get_focused = tool({
  description:
    "Get the currently focused/active UI element. Useful for determining which control has keyboard input focus.",
  args: {},
  async execute() {
    const result: any = await sendCommand("get_focused_element", {})
    if (!result.success) return JSON.stringify({ error: result.error })
    if (!result.data) return "No focused element found."
    const el = result.data
    return JSON.stringify(
      {
        name: el.name,
        automationId: el.automationId,
        controlType: el.controlType,
        className: el.className,
        isEnabled: el.isEnabled,
        runtimeId: el.runtimeId,
        patterns: el.patterns,
        value: el.value,
        processId: el.processId,
      },
      null,
      2,
    )
  },
})

export const uia_wait = tool({
  description: "Wait for a UI element matching the given criteria to appear (up to the specified timeout).",
  args: {
    name: tool.schema.string().optional().describe("Element name to wait for"),
    automationId: tool.schema.string().optional().describe("Automation ID to wait for"),
    controlType: tool.schema.string().optional().describe("Control type to wait for"),
    timeoutMs: tool.schema.number().optional().default(5000).describe("Timeout in milliseconds (default: 5000)"),
  },
  async execute(args) {
    const params: Record<string, unknown> = {
      name: args.name ?? null,
      automationId: args.automationId ?? null,
      controlType: args.controlType ?? null,
      timeoutMs: args.timeoutMs ?? 5000,
      searchDescendants: true,
      maxDepth: 10,
    }
    const result: any = await sendCommand("wait_for_element", params)
    if (!result.success) return JSON.stringify({ error: result.error })
    if (result.data?.found) return `Element found within ${args.timeoutMs ?? 5000}ms.`
    return `Element NOT found within ${args.timeoutMs ?? 5000}ms.`
  },
})

export const uia_scroll_into_view = tool({
  description: "Scroll a UI element into view by its runtimeId.",
  args: {
    runtimeId: tool.schema.string().describe("Runtime ID of the element to scroll into view"),
  },
  async execute(args) {
    const result: any = await sendCommand("scroll_into_view", { runtimeId: args.runtimeId })
    if (!result.success) return JSON.stringify({ error: result.error })
    return `Element scrolled into view.`
  },
})

export const uia_get_patterns = tool({
  description: "Get the list of UI Automation patterns supported by a specific element (Invoke, Value, Toggle, etc.).",
  args: {
    runtimeId: tool.schema.string().describe("Runtime ID of the element"),
  },
  async execute(args) {
    const result: any = await sendCommand("get_patterns", { runtimeId: args.runtimeId })
    if (!result.success) return JSON.stringify({ error: result.error })
    return JSON.stringify(result.data, null, 2)
  },
})
