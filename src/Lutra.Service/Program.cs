using Lutra.Core;
using Lutra.Service;

Directory.CreateDirectory(ControlMessages.DataDirectory);
try
{
    if (args.Contains("--console"))
    {
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
        await new Controller().RunAsync(stop.Token);
    }
    else WindowsServiceHost.Run(token => new Controller().RunAsync(token));
    return 0;
}
catch (Exception e)
{
    try { File.AppendAllText(Path.Combine(ControlMessages.DataDirectory, "service.log"), DateTimeOffset.UtcNow + " " + e + Environment.NewLine); }
    catch (IOException) { }
    return 1;
}
