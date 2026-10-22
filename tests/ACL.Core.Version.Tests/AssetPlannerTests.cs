using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version;
using Xunit;

namespace ACL.Core.Version.Tests;

public class AssetPlannerTests
{
    private const string IndexJson = """
    {
      "objects": {
        "icons/icon_16x16.png": { "hash": "abcdef0123456789abcdef0123456789abcdef01", "size": 256 },
        "minecraft/sounds/a.ogg": { "hash": "0123456789abcdef0123456789abcdef01234567", "size": 1024 },
        "minecraft/sounds/b.ogg": { "hash": "abcdef0123456789abcdef0123456789abcdef01", "size": 256 }
      }
    }
    """;

    [Fact]
    public void Parse_ReadsObjects()
    {
        Result<AssetIndex> result = AssetIndexParser.Parse(IndexJson, "5");

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        Assert.Equal("5", result.Value!.Id);
        Assert.Equal(3, result.Value!.Objects.Count);
        Assert.Equal(256, result.Value!.Objects["icons/icon_16x16.png"].Size);
    }

    [Fact]
    public void Parse_MissingObjects_Fails()
    {
        Result<AssetIndex> result = AssetIndexParser.Parse("""{ "id": "5" }""", "5");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ManifestCorrupt, result.Code);
    }

    [Fact]
    public void Plan_AddressesByHashAndDeduplicates()
    {
        AssetIndex index = AssetIndexParser.Parse(IndexJson, "5").Value!;
        InstallTargets targets = InstallTargets.ForGameDir("D:/mc");

        IReadOnlyList<DownloadItem> downloads = new AssetPlanner().Plan(index, targets);

        Assert.Equal(2, downloads.Count);   // 第三个对象与第一个哈希相同，去重
        DownloadItem icon = downloads.Single(d => d.TargetPath.EndsWith("abcdef0123456789abcdef0123456789abcdef01", StringComparison.Ordinal));
        Assert.Equal(new Uri("https://resources.download.minecraft.net/ab/abcdef0123456789abcdef0123456789abcdef01"), icon.Primary);
        Assert.Equal(Path.Combine(targets.AssetsDir, "objects", "ab", "abcdef0123456789abcdef0123456789abcdef01"), icon.TargetPath);
        Assert.Equal(HashKind.Sha1, icon.Hash!.Kind);
    }
}