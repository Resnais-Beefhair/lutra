using System.IO.Compression;
using System.Text.Json;
using Lutra.Core;

internal static class UpdateTests
{
    private static void Assert(bool value, string message = "Update assertion failed") { if (!value) throw new Exception(message); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Unsafe update was accepted."); }
    public static void Run(Action<string, Action> test)
    {
        test("Update semantic versions order numeric previews, stable releases and build metadata", () => {
            string[] versions = ["0.4.0", "0.5.0-preview.1", "0.5.0-preview.2", "0.5.0-preview.10", "0.5.0-rc.1", "0.5.0", "1.0.0", "10.0.0"];
            for (int i = 1; i < versions.Length; i++) Assert(ReleaseVersion.Parse(versions[i]).CompareTo(ReleaseVersion.Parse(versions[i-1])) > 0, versions[i]);
            Assert(ReleaseVersion.Parse("v1.0.0+abc").CompareTo(ReleaseVersion.Parse("1.0.0+xyz")) == 0);
            foreach (string invalid in new[] { "1.0", "1.00.0", "1.0.0-preview.01", "1.0.0-", "1.0.0\r\n", "1.0.0/../../evil", "1.0.0-preview..1" }) Assert(!ReleaseVersion.TryParse(invalid, out _), invalid);
        });
        test("Update channel selects highest eligible version, never downgrades", () => {
            ReleaseUpdate Make(string version, bool preview = false) => new("v" + version, ReleaseVersion.Parse(version), "Lutra", "", new(Releases.ProjectUrl), new(Releases.ProjectUrl), "app.zip", 100, new('A',64), preview || ReleaseVersion.Parse(version).IsPreview);
            var releases = new[] { Make("0.4.0"), Make("0.5.0-preview.2"), Make("0.5.0-preview.10"), Make("0.4.1") };
            Assert(Releases.Latest(releases, ReleaseVersion.Parse("0.4.0"), false)?.Version.Text == "0.4.1");
            Assert(Releases.Latest(releases, ReleaseVersion.Parse("0.4.0"), true)?.Version.Text == "0.5.0-preview.10");
            Assert(Releases.Latest(releases, ReleaseVersion.Parse("0.5.0"), true) is null);
            Assert(Releases.Latest([Make("0.5.0", true)], ReleaseVersion.Parse("0.4.0"), false) is null);
        });
        test("Update metadata binds repository, tag, asset name, size and GitHub digest", () => {
            string tag = "v0.5.0-preview.1", name = "Lutra-0.5.0-preview.1-win-x64.zip";
            object Metadata(string? digest, string? download = null, long size = 100) => new {
                draft=false, tag_name=tag, name="Lutra", body="Release", prerelease=true, html_url=Releases.ProjectUrl + "/releases/tag/" + tag,
                assets=new[] { new { name, state="uploaded", size, digest, browser_download_url=download ?? Releases.ProjectUrl + "/releases/download/" + tag + "/" + name } }
            };
            ReleaseUpdate? Read(object value) { using var json = JsonDocument.Parse(JsonSerializer.Serialize(value)); return Releases.Read(json.RootElement); }
            string hash = "sha256:" + new string('a',64);
            Assert(Read(Metadata(hash))?.Sha256 == new string('A',64));
            Assert(Read(Metadata(null)) is null); Assert(Read(Metadata("sha256:abc")) is null);
            Assert(Read(Metadata(hash, "https://github.com.evil.test/asset.zip")) is null);
            Assert(Read(Metadata(hash, Releases.ProjectUrl + "/releases/download/v0.4.0/" + name)) is null);
            Assert(Read(Metadata(hash, size: Releases.MaximumDownload + 1)) is null);
        });
        test("Update archive accepts package wrapper and rejects traversal, links, collisions and missing executables", () => {
            string[] required = ["Lutra.exe", "Lutra.dll", "Lutra.Service.exe", "Lutra.Service.dll", "tools/Update-Lutra.ps1", "tools/Recover-LutraUpdate.ps1"];
            void Validate(string prefix, string? unsafeName = null, int? attributes = null, bool omit = false)
            {
                using var stream = new MemoryStream();
                using (var writer = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    foreach (var file in required.Skip(omit ? 1 : 0)) writer.CreateEntry(prefix + file);
                    if (unsafeName != null) { var entry = writer.CreateEntry(unsafeName); if (attributes.HasValue) entry.ExternalAttributes = attributes.Value; }
                }
                stream.Position = 0; using var reader = new ZipArchive(stream, ZipArchiveMode.Read);
                Assert(Releases.ValidateArchive(reader) == prefix);
            }
            Validate(""); Validate("Lutra-release/");
            foreach (string name in new[] { "../escape", "C:/escape", "/escape", "tools/../../escape", "tools/evil:stream", "LUTRA.EXE", "CON.txt", "tools/evil. ", "a//b", "a\\..\\b" }) Reject(() => Validate("", name));
            Reject(() => Validate("", "link", 0xA000 << 16)); Reject(() => Validate("", "reparse", 0x400)); Reject(() => Validate("", omit:true));
        });
    }
}
