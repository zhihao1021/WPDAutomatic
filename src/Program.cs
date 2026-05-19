using WPDAutomatic.Commands;

var router = new CommandRouter();

Console.Error.WriteLine("WPDAutomatic ready. Send JSON commands via stdin.");

while (true)
{
    var line = Console.ReadLine();
    if (line is null) break;
    if (string.IsNullOrWhiteSpace(line)) continue;

    var response = router.ProcessCommand(line);
    Console.WriteLine(response);
}
