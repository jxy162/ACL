using System.IO.Compression;
using System.Text;
using ACL.Core.Domain;
using ACL.Core.Download;
using ACL.Core.Modpack;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Modpack.Tests;

public class ModpackInstallerTests
{
    private const string ArchivePath = "D:/packs/test.mrpack";
    private const string InstanceDir = "D:/mc/instances/pack";
    private const string FileUrl = "https://cdn.modrinth.com/data/AAA/versions/1/foo.jar";

    private static byte[] ModFileBytes => TestData.MakeBytes(64, 5);

    private static string ModrinthIndex(byte[] modBytes) => """
    {
      "formatVersion": 1,
      "name": "Test Pack",
      "versionId": "1.0.0",
      "dependencies": { "minecraft": "1.20.1", "fabric-loader": "0.15.0" },
      "files": [
        {
          "path": "mods/foo.jar",
          "hashes": { "sha1": "SHA1PLACE", "sha512": "SHA512PLACE" },
          "downloads": [ "URLPLACE" ],
          "fileSize": FILELEN,
          "env": { "client": "required", "server": "required" }
        }
      ]
    }
    """.Replace("SHA1PLACE", TestData.Sha1Of(modBytes).Value)
        .Replace("SHA512PLACE", Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(modBytes)).ToLowerInvariant())
        .Replace("URLPLACE", FileUrl)
        .Replace("FILELEN", modBytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private const string CurseForgeManifest = """
    {
      "minecraft": { "version": "1.20.1", "modLoaders": [ { "id": "forge-47.2.0", "primary": true } ] },
      "name": "CF Pack",
      "version": "2.0",
      "files": [ { "projectID": 238222, "fileID": 456789, "required": true } ],
      "overrides": "overrides"
    }
    """;

    [Fact]
    public void Parse_Modrinth_ReadsManifest()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile(ArchivePath, MakeZip(("modrinth.index.json", ModrinthIndex(ModFileBytes))));
        ModpackInstaller installer = new(fs, new DownloadService(new StubHttpTransport(), fs));

        var result = installer.Parse(ArchivePath);

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        ModpackManifest manifest = result.Value!;
        Assert.Equal(ModpackFormat.Modrinth, manifest.Format);
        Assert.Equal("Test Pack", manifest.Name);
        Assert.Equal("1.20.1", manifest.MinecraftVersion);
        Assert.Contains(manifest.Loaders, l => l.Kind == ModLoaderKind.Fabric && l.Version == "0.15.0");
        ModpackFile file = Assert.Single(manifest.Files);
        Assert.True(file.IsResolved);
        Assert.Equal("mods/foo.jar", file.RelativePath);
        Assert.Equal(HashKind.Sha512, file.PreferredHash!.Kind);
    }

    [Fact]
    public void Parse_CurseForge_ReadsLoadersAndUnresolvedFiles()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile(ArchivePath, MakeZip(("manifest.json", CurseForgeManifest)));
        ModpackInstaller installer = new(fs, new DownloadService(new StubHttpTransport(), fs));

        var result = installer.Parse(ArchivePath);

        Assert.True(result.IsSuccess);
        ModpackManifest manifest = result.Value!;
        Assert.Equal(ModpackFormat.CurseForge, manifest.Format);
        Assert.Contains(manifest.Loaders, l => l.Kind == ModLoaderKind.Forge && l.Version == "47.2.0");
        ModpackFile file = Assert.Single(manifest.UnresolvedFiles);
        Assert.Equal(238222, file.ProjectId);
        Assert.Equal(456789, file.FileId);
        Assert.Equal("overrides", manifest.OverridesPrefix);
    }

    [Fact]
    public void Parse_NoManifest_FallsBackToGeneric()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile(ArchivePath, MakeZip(("config/x.txt", "data")));
        ModpackInstaller installer = new(fs, new DownloadService(new StubHttpTransport(), fs));

        var result = installer.Parse(ArchivePath);

        Assert.True(result.IsSuccess);
        Assert.Equal(ModpackFormat.Generic, result.Value!.Format);
    }

    [Fact]
    public async Task InstallAsync_CopiesOverridesAndDownloadsResolvedFiles()
    {
        byte[] modBytes = ModFileBytes;
        InMemoryFileSystem fs = new();
        StubHttpTransport http = new StubHttpTransport().OnContent(FileUrl, modBytes);
        fs.WriteFile(ArchivePath, MakeZip(
            ("modrinth.index.json", ModrinthIndex(modBytes)),
            ("overrides/config/options.txt", "hello"),
            ("overrides/../evil.txt", "evil")));
        ModpackInstaller installer = new(fs, new DownloadService(http, fs));

        ModpackManifest manifest = installer.Parse(ArchivePath).Value!;
        var result = await installer.InstallAsync(manifest, ArchivePath, InstanceDir);

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        Assert.Equal(1, result.Value!.OverridesCopied);                       // ../evil.txt 被拒
        Assert.Equal("hello", Encoding.UTF8.GetString(fs.ReadAllBytes(InstanceDir + "/config/options.txt")));
        Assert.Equal(modBytes, fs.ReadAllBytes(InstanceDir + "/mods/foo.jar"));
        Assert.False(fs.FileExists("D:/mc/instances/evil.txt"));
    }

    private static byte[] MakeZip(params (string Name, string Content)[] entries)
    {
        using MemoryStream stream = new();
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name);
                using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }
}