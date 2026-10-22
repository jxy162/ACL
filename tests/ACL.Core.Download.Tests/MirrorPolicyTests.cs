using ACL.Core.Abstractions;
using Xunit;

namespace ACL.Core.Download.Tests;

public class MirrorPolicyTests
{
    private static readonly MirrorPolicy Policy = new();

    [Fact]
    public void VersionManifest_IsRewritten()
    {
        Uri origin = new("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json");
        IReadOnlyList<Uri> c = Policy.Resolve(MirrorKind.VersionManifest, origin);

        Assert.Equal(origin, c[0]);
        Assert.Equal(new Uri("https://bmclapi2.bangbang93.com/mc/game/version_manifest.json"), c[1]);
    }

    [Fact]
    public void VersionMeta_HostIsReplaced()
    {
        Uri origin = new("https://piston-meta.mojang.com/mc/game/1.20.1.json");
        IReadOnlyList<Uri> c = Policy.Resolve(MirrorKind.VersionMeta, origin);

        Assert.Equal(origin, c[0]);
        Assert.Equal(new Uri("https://bmclapi2.bangbang93.com/mc/game/1.20.1.json"), c[1]);
    }

    [Fact]
    public void Library_HasTwoMirrorCandidates()
    {
        Uri origin = new("https://libraries.minecraft.net/net/minecraft/client/1.20.1/client-1.20.1.jar");
        IReadOnlyList<Uri> c = Policy.Resolve(MirrorKind.Library, origin);

        Assert.Equal(origin, c[0]);
        Assert.Contains(new Uri("https://bmclapi2.bangbang93.com/maven/net/minecraft/client/1.20.1/client-1.20.1.jar"), c);
        Assert.Contains(new Uri("https://bmclapi2.bangbang93.com/libraries/net/minecraft/client/1.20.1/client-1.20.1.jar"), c);
    }

    [Fact]
    public void Asset_IsRewrittenAndForcedToHttps()
    {
        Uri origin = new("http://resources.download.minecraft.net/ab/abcd");
        IReadOnlyList<Uri> c = Policy.Resolve(MirrorKind.Asset, origin);

        Assert.Equal("https", c[0].Scheme);
        Assert.Contains(new Uri("https://bmclapi2.bangbang93.com/assets/ab/abcd"), c);
    }

    [Fact]
    public void FabricAndNeoForge_AreRewritten()
    {
        IReadOnlyList<Uri> fabric = Policy.Resolve(MirrorKind.FabricMeta, new Uri("https://meta.fabricmc.net/v2/versions/loader"));
        Assert.Contains(new Uri("https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader"), fabric);

        IReadOnlyList<Uri> neo = Policy.Resolve(
            MirrorKind.NeoForgeMeta,
            new Uri("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge"));
        Assert.Contains(
            new Uri("https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge"),
            neo);
    }

    [Fact]
    public void QuiltWithoutMirrorSupport_FallsBackToOfficialOnly()
    {
        Uri origin = new("https://meta.quiltmc.org/v3/versions/loader");
        IReadOnlyList<Uri> c = Policy.Resolve(MirrorKind.QuiltMeta, origin);

        Assert.Single(c);
        Assert.Equal(origin, c[0]);
    }

    [Fact]
    public void UnknownHost_ReturnsOriginOnly()
    {
        Uri origin = new("https://example.invalid/some/file.json");
        IReadOnlyList<Uri> c = Policy.Resolve(MirrorKind.VersionMeta, origin);

        Assert.Single(c);
        Assert.Equal(origin, c[0]);
    }

    [Fact]
    public void PreferOfficialOverride_PlacesMirrorFirst()
    {
        MirrorPolicy policy = new(
            providers: null,
            preferOfficial: true,
            preferOfficialOverrides: new Dictionary<MirrorKind, bool> { [MirrorKind.VersionManifest] = false });

        Uri origin = new("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json");
        IReadOnlyList<Uri> c = policy.Resolve(MirrorKind.VersionManifest, origin);

        Assert.False(policy.PreferOfficial(MirrorKind.VersionManifest));
        Assert.Equal(new Uri("https://bmclapi2.bangbang93.com/mc/game/version_manifest.json"), c[0]);
    }
}