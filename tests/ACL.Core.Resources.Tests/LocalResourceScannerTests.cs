using System.IO.Compression;
using System.Text;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Resources;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Resources.Tests;

public class LocalResourceScannerTests
{
    private const string FabricJson = """
    {
      "schemaVersion": 1,
      "id": "sodium",
      "name": "Sodium",
      "version": "0.5.3",
      "authors": [ "jellysquid3", { "name": "Other" } ],
      "depends": { "fabricloader": ">=0.14.0", "minecraft": "1.20.x" }
    }
    """;

    private const string ForgeToml = """
    modLoader="javafml"
    loaderVersion="[47,)"
    license="MIT"

    [[mods]]
    modId="jei"
    version="15.2.0.27"
    displayName="Just Enough Items"
    authors="mezz"

    [[dependencies.jei]]
    modId="minecraft"
    mandatory=true

    [[dependencies.jei]]
    modId="forge"
    mandatory=true
    """;

    private const string LegacyJson = """
    [ { "modid": "oldmod", "name": "Old Mod", "version": "1.0", "authorList": [ "someone" ] } ]
    """;

    [Fact]
    public void Scan_Fabric_ReadsMetadata()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile("D:/mc/mods/sodium.jar", MakeZip(("fabric.mod.json", FabricJson)));
        LocalResourceScanner scanner = new(fs);

        Result<LocalResourceInfo> result = scanner.Scan("D:/mc/mods/sodium.jar");

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        Assert.Equal(ModMetadataFormat.Fabric, result.Value!.Format);
        Assert.Equal("sodium", result.Value!.ModId);
        Assert.Equal("Sodium", result.Value!.Name);
        Assert.Equal("0.5.3", result.Value!.Version);
        Assert.Equal(["jellysquid3", "Other"], result.Value!.Authors);
        Assert.Contains("fabricloader", result.Value!.Dependencies);
        Assert.Contains("minecraft", result.Value!.Dependencies);
    }

    [Fact]
    public void Scan_ForgeToml_ReadsMetadataAndDependencies()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile("D:/mc/mods/jei.jar", MakeZip(("META-INF/mods.toml", ForgeToml)));
        LocalResourceScanner scanner = new(fs);

        Result<LocalResourceInfo> result = scanner.Scan("D:/mc/mods/jei.jar");

        Assert.True(result.IsSuccess);
        Assert.Equal(ModMetadataFormat.ForgeToml, result.Value!.Format);
        Assert.Equal("jei", result.Value!.ModId);
        Assert.Equal("Just Enough Items", result.Value!.Name);
        Assert.Equal("15.2.0.27", result.Value!.Version);
        Assert.Contains("minecraft", result.Value!.Dependencies);
        Assert.Contains("forge", result.Value!.Dependencies);
    }

    [Fact]
    public void Scan_LegacyMcModInfo_ReadsMetadata()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile("D:/mc/mods/old.jar", MakeZip(("mcmod.info", LegacyJson)));
        LocalResourceScanner scanner = new(fs);

        Result<LocalResourceInfo> result = scanner.Scan("D:/mc/mods/old.jar");

        Assert.True(result.IsSuccess);
        Assert.Equal(ModMetadataFormat.LegacyMcModInfo, result.Value!.Format);
        Assert.Equal("oldmod", result.Value!.ModId);
        Assert.Equal(["someone"], result.Value!.Authors);
    }

    [Fact]
    public void Scan_PlainJar_IsUnknown()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile("D:/mc/mods/plain.jar", MakeZip(("com/example/Main.class", "bytecode")));
        LocalResourceScanner scanner = new(fs);

        Result<LocalResourceInfo> result = scanner.Scan("D:/mc/mods/plain.jar");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsRecognized);
    }

    [Fact]
    public void Scan_MissingFile_ReturnsNotFound()
    {
        Result<LocalResourceInfo> result = new LocalResourceScanner(new InMemoryFileSystem()).Scan("D:/missing.jar");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.Code);
    }

    [Fact]
    public void ScanDirectory_ScansAllJars()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile("D:/mc/mods/sodium.jar", MakeZip(("fabric.mod.json", FabricJson)));
        fs.WriteFile("D:/mc/mods/readme.txt", "not a jar");
        LocalResourceScanner scanner = new(fs);

        Result<IReadOnlyList<LocalResourceInfo>> result = scanner.ScanDirectory("D:/mc/mods");

        Assert.True(result.IsSuccess);
        LocalResourceInfo single = Assert.Single(result.Value!);
        Assert.Equal("sodium", single.ModId);
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