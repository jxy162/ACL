using ACL.Core.Domain;
using Xunit;

namespace ACL.Core.Domain.Tests;

public class LoaderNamesTests
{
    [Theory]
    [InlineData(ModLoaderKind.Forge, "forge")]
    [InlineData(ModLoaderKind.NeoForge, "neoforge")]
    [InlineData(ModLoaderKind.Fabric, "fabric")]
    [InlineData(ModLoaderKind.Quilt, "quilt")]
    public void ModrinthNames_RoundTrip(ModLoaderKind kind, string name)
    {
        Assert.Equal(name, LoaderNames.ToModrinth(kind));
        Assert.Equal(kind, LoaderNames.FromModrinth(name));
    }

    [Fact]
    public void ModrinthNames_UnsupportedIsNull()
    {
        Assert.Null(LoaderNames.ToModrinth(ModLoaderKind.OptiFine));
        Assert.Equal(ModLoaderKind.None, LoaderNames.FromModrinth("unknown"));
    }

    [Theory]
    [InlineData(ModLoaderKind.Forge, 1)]
    [InlineData(ModLoaderKind.LiteLoader, 3)]
    [InlineData(ModLoaderKind.Fabric, 4)]
    [InlineData(ModLoaderKind.Quilt, 5)]
    [InlineData(ModLoaderKind.NeoForge, 6)]
    public void CurseForgeTypes_RoundTrip(ModLoaderKind kind, int value)
    {
        Assert.Equal(value, LoaderNames.ToCurseForge(kind));
        Assert.Equal(kind, LoaderNames.FromCurseForge(value));
    }

    [Theory]
    [InlineData(ResourceKind.Mod, 6)]
    [InlineData(ResourceKind.ResourcePack, 12)]
    [InlineData(ResourceKind.ShaderPack, 6552)]
    [InlineData(ResourceKind.Modpack, 4471)]
    public void ClassIds_MapBothWays(ResourceKind kind, int classId)
    {
        Assert.Equal(classId, CurseForgeClassIds.ForKind(kind));
        Assert.Equal(kind, CurseForgeClassIds.KindFor(classId));
    }
}