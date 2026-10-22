using ACL.Core.Domain;
using Xunit;

namespace ACL.Core.Domain.Tests;

public class ModpackLoaderIdsTests
{
    [Theory]
    [InlineData("forge-47.2.0", ModLoaderKind.Forge, "47.2.0")]
    [InlineData("neoforge-21.1.0", ModLoaderKind.NeoForge, "21.1.0")]
    [InlineData("fabric-0.15.0", ModLoaderKind.Fabric, "0.15.0")]
    [InlineData("fabric-loader-0.15.0", ModLoaderKind.Fabric, "0.15.0")]
    [InlineData("quilt-loader-0.20.0", ModLoaderKind.Quilt, "0.20.0")]
    public void Parse_RecognizesKnownLoaders(string id, ModLoaderKind kind, string version)
    {
        ModpackLoader? loader = ModpackLoaderIds.Parse(id);

        Assert.NotNull(loader);
        Assert.Equal(kind, loader!.Kind);
        Assert.Equal(version, loader.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("forge")]
    [InlineData("unknown-1.0")]
    public void Parse_Invalid_ReturnsNull(string? id)
    {
        Assert.Null(ModpackLoaderIds.Parse(id));
    }
}