if (args.Length > 0 && args[0].Equals("--mcp", StringComparison.OrdinalIgnoreCase))
{
    using var server = new WPDAutomatic.Mcp.McpServer();
    await server.RunAsync();
    return;
}

// Default: stdin/stdout JSON command protocol
var router = new WPDAutomatic.Commands.CommandRouter();

Console.Error.WriteLine("WPDAutomatic ready. Send JSON commands via stdin.");

while (true)
{
    var line = Console.ReadLine();
    if (line is null) break;
    if (string.IsNullOrWhiteSpace(line)) continue;

    var response = router.ProcessCommand(line);
    Console.WriteLine(response);
}
