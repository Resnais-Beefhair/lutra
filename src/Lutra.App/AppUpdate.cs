using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lutra.App;

public sealed record StagedUpdate(ReleaseUpdate Release, string ArchivePath);

public static class AppUpdate
{
    public static string CurrentVersion => (typeof(AppUpdate).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];
    public static string InstallDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Lutra");
    public static bool IsInstalled => string.Equals(Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar), InstallDirectory, StringComparison.OrdinalIgnoreCase)
        && File.Exists(Path.Combine(InstallDirectory, "tools", "Update-Lutra.ps1"));
    private static HttpClient Client()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Lutra/" + CurrentVersion);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }
    public static async Task<ReleaseUpdate?> CheckAsync(bool includePreviews, CancellationToken token = default)
    {
        using var client = Client(); var releases = new List<ReleaseUpdate>();
        for (int page = 1; page <= 10; page++)
        {
            using var response = await client.GetAsync($"https://api.github.com/repos/{Releases.Repository}/releases?per_page=100&page={page}", token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
            foreach (var entry in json.RootElement.EnumerateArray()) { var release = Releases.Read(entry); if (release is not null) releases.Add(release); }
            if (json.RootElement.GetArrayLength() < 100) break;
        }
        return Releases.Latest(releases, ReleaseVersion.Parse(CurrentVersion), includePreviews);
    }
    public static async Task<StagedUpdate> DownloadAsync(ReleaseUpdate release, IProgress<double>? progress = null, CancellationToken token = default)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lutra", "updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder); string path = Path.Combine(folder, release.AssetName);
        try
        {
            using var client = Client();
            using var response = await client.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length != release.Size) throw new InvalidDataException("Release download size changed. Check for updates again.");
            await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                long total = 0; byte[] buffer = new byte[81920]; int read;
                while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    total += read; if (total > release.Size || total > Releases.MaximumDownload) throw new InvalidDataException("Release download exceeded its expected size.");
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false); hash.AppendData(buffer, 0, read); progress?.Report((double)total / release.Size);
                }
                if (total != release.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Release checksum verification failed.");
            }
            using (var archive = ZipFile.OpenRead(path)) Releases.ValidateArchive(archive);
            return new(release, path);
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
    }
    /// <summary>Returns after UAC approval. The caller must immediately shut down its monitor window/application.</summary>
    public static void StartInstall(StagedUpdate update)
    {
        if (!IsInstalled) throw new InvalidOperationException("Install Lutra in Program Files before using the updater.");
        string job = Guid.NewGuid().ToString("N");
        string updater = Path.Combine(InstallDirectory, "tools", "Update-Lutra.ps1");
        string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string powershell = Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var command = new ProcessStartInfo(powershell) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = systemDirectory };
        foreach (string arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", updater, "-Archive", update.ArchivePath,
            "-Tag", update.Release.Tag, "-ExpectedSha256", update.Release.Sha256, "-ParentId", Environment.ProcessId.ToString(CultureInfo.InvariantCulture), "-JobId", job }) command.ArgumentList.Add(arg);
        using var elevated = Process.Start(command) ?? throw new IOException("The update process could not start.");
        // The watcher is launched with the monitor's original token, so the replacement monitor is never elevated.
        string result = Path.Combine(ControlMessages.DataDirectory, "updates", job, "result.json");
        static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
        string script = "$ErrorActionPreference='Stop'; $until=[DateTime]::UtcNow.AddMinutes(12); while([DateTime]::UtcNow -lt $until) { if(Test-Path -LiteralPath " + Quote(result) + ") { $result=Get-Content -LiteralPath " + Quote(result) + " -Raw | ConvertFrom-Json; if(-not $result.RecoveryPending) { Start-Process -FilePath " + Quote(Path.Combine(InstallDirectory, "Lutra.exe")) + " -WorkingDirectory " + Quote(InstallDirectory) + " -ArgumentList '--update-result'," + Quote(job) + "; exit } }; Start-Sleep -Seconds 2 }";
        var watcher = new ProcessStartInfo(powershell) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = systemDirectory };
        foreach (string arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }) watcher.ArgumentList.Add(arg);
        try { Process.Start(watcher)?.Dispose(); }
        catch { /* The protected result remains readable from About after the user opens Lutra again. */ }
    }
    public static string? ReadUpdateResult(string job)
    {
        if (job.Length != 32 || job.Any(c => !Uri.IsHexDigit(c))) return null;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(ControlMessages.DataDirectory, "updates", job, "result.json")));
            return json.RootElement.GetProperty("Message").GetString();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
}
