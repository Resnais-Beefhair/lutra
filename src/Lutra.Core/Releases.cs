using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lutra.Core;

/// <summary>Semantic precedence, including numeric prerelease identifiers (preview.10 > preview.2).</summary>
public sealed record ReleaseVersion : IComparable<ReleaseVersion>
{
    private static readonly Regex Pattern = new(@"\Av?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z", RegexOptions.CultureInvariant);
    public string Text { get; }
    public bool IsPreview => preview.Length > 0;
    private readonly string[] numbers;
    private readonly string[] preview;
    private ReleaseVersion(string text, string[] numbers, string[] preview) { Text = text.TrimStart('v'); this.numbers = numbers; this.preview = preview; }
    public static bool TryParse(string? text, out ReleaseVersion? version)
    {
        version = null;
        if (text is null || text.Length > 160) return false;
        var match = Pattern.Match(text);
        if (!match.Success) return false;
        var pre = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : [];
        if (pre.Any(p => p.All(char.IsAsciiDigit) && p.Length > 1 && p[0] == '0')) return false;
        version = new(text, [match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value], pre); return true;
    }
    public static ReleaseVersion Parse(string text) => TryParse(text, out var version) ? version! : throw new InvalidDataException("Invalid release version.");
    private static int Numeric(string a, string b) => a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) return 1;
        for (int i = 0; i < 3; i++) { int c = Numeric(numbers[i], other.numbers[i]); if (c != 0) return c; }
        if (preview.Length == 0 || other.preview.Length == 0) return (preview.Length == 0 ? 1 : 0).CompareTo(other.preview.Length == 0 ? 1 : 0);
        for (int i = 0; i < Math.Min(preview.Length, other.preview.Length); i++)
        {
            bool a = preview[i].All(char.IsAsciiDigit), b = other.preview[i].All(char.IsAsciiDigit);
            int c = a && b ? Numeric(preview[i], other.preview[i]) : a != b ? (a ? -1 : 1) : string.CompareOrdinal(preview[i], other.preview[i]);
            if (c != 0) return c;
        }
        return preview.Length.CompareTo(other.preview.Length);
    }
    public override string ToString() => Text;
}

public sealed record ReleaseUpdate(string Tag, ReleaseVersion Version, string Name, string Notes, Uri PageUrl,
    Uri DownloadUrl, string AssetName, long Size, string Sha256, bool Prerelease);

public static class Releases
{
    public const string Repository = "Resnais-Beefhair/lutra";
    public const string ProjectUrl = "https://github.com/" + Repository;
    public const long MaximumDownload = 256L * 1024 * 1024;
    public const long MaximumExpanded = 1024L * 1024 * 1024;
    public const int MaximumEntries = 5000;
    public static ReleaseUpdate? Read(JsonElement release)
    {
        if (release.GetProperty("draft").GetBoolean()) return null;
        string tag = release.GetProperty("tag_name").GetString() ?? "";
        if (!ReleaseVersion.TryParse(tag, out var version)) return null;
        string name = "Lutra-" + version!.Text + "-win-x64.zip";
        string prefix = ProjectUrl + "/releases/";
        string page = release.GetProperty("html_url").GetString() ?? "";
        if (page != prefix + "tag/" + tag) return null;
        var assets = release.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == name).ToArray();
        if (assets.Length != 1) return null;
        var asset = assets[0];
        long size = asset.GetProperty("size").GetInt64();
        string download = asset.GetProperty("browser_download_url").GetString() ?? "";
        string digest = asset.TryGetProperty("digest", out var hash) ? hash.GetString() ?? "" : "";
        if (asset.GetProperty("state").GetString() != "uploaded" || size <= 0 || size > MaximumDownload ||
            download != prefix + "download/" + tag + "/" + name || !Regex.IsMatch(digest, @"\Asha256:[a-fA-F0-9]{64}\z", RegexOptions.CultureInvariant)) return null;
        return new(tag, version, release.GetProperty("name").GetString() ?? tag,
            release.GetProperty("body").GetString() ?? "", new(page), new(download), name, size, digest[7..].ToUpperInvariant(),
            release.GetProperty("prerelease").GetBoolean() || version.IsPreview);
    }
    public static ReleaseUpdate? Latest(IEnumerable<ReleaseUpdate> candidates, ReleaseVersion current, bool includePreviews) =>
        candidates.Where(r => (includePreviews || !r.Prerelease) && r.Version.CompareTo(current) > 0)
            .OrderByDescending(r => r.Version).FirstOrDefault();

    /// <summary>Validate paths before extracting anything; allows a flat package or a single enclosing directory.</summary>
    public static string ValidateArchive(ZipArchive archive)
    {
        if (archive.Entries.Count is 0 or > MaximumEntries) throw new InvalidDataException("Invalid archive entry count.");
        long expanded = 0; var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var files = new List<string>();
        foreach (var entry in archive.Entries)
        {
            string path = entry.FullName.Replace('\\', '/');
            var parts = path.TrimEnd('/').Split('/');
            int unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || parts.Any(p => p.Length == 0 || p is "." or ".." ||
                p.Contains(':') || p.EndsWith('.') || p.EndsWith(' ') || p.Any(c => c < 32 || "<>\"|?*".Contains(c)) ||
                Regex.IsMatch(p.Split('.')[0], @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) ||
                path.Length > 220 || unixType == 0xA000 || (entry.ExternalAttributes & 0x400) != 0 || !paths.Add(path.TrimEnd('/')))
                throw new InvalidDataException("Unsafe or duplicate archive path.");
            expanded = checked(expanded + entry.Length);
            if (entry.Length > MaximumDownload || expanded > MaximumExpanded) throw new InvalidDataException("Archive is too large.");
            if (!path.EndsWith('/')) files.Add(path);
        }
        if (files.Count == 0) throw new InvalidDataException("Release package contains no files.");
        string prefix = files.Contains("Lutra.exe", StringComparer.OrdinalIgnoreCase) ? "" :
            files.Select(f => f.Split('/')[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1 ? files[0].Split('/')[0] + "/" : throw new InvalidDataException("Release package root is missing.");
        foreach (string required in new[] { "Lutra.exe", "Lutra.dll", "Lutra.Service.exe", "Lutra.Service.dll", "tools/Update-Lutra.ps1", "tools/Recover-LutraUpdate.ps1" })
            if (!files.Contains(prefix + required, StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("Release file missing: " + required);
        return prefix;
    }
}
