using Lutra.Core;
using Lutra.Service;

try
{
    if (args.Contains("--refresh-gpu"))
    {
        try
        {
            var reply = await ControlMessages.RequestAsync(new("refresh-gpu"));
            Console.WriteLine(reply.Message);
            return reply.Success ? 0 : 1;
        }
        catch (Exception e) { Console.Error.WriteLine(e.GetBaseException().Message); return 1; }
    }
    Directory.CreateDirectory(ControlMessages.DataDirectory);
    if (args.Contains("--console"))
    {
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
        await new Controller().RunAsync(stop.Token, allowGpuProcessRestart: false);
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
