using ACL.Core.Domain;
using Xunit;

namespace ACL.Core.Domain.Tests;

public class MavenCoordinatesTests
{
    [Theory]
    [InlineData("com.mojang:logging:1.1.1", "com/mojang/logging/1.1.1/logging-1.1.1.jar")]
    [InlineData("org.lwjgl:lwjgl:3.3.1:natives-windows", "org/lwjgl/lwjgl/3.3.1/lwjgl-3.3.1-natives-windows.jar")]
    [InlineData("net.minecraftforge:forge:1.20.1-47.2.0:universal@zip", "net/minecraftforge/forge/1.20.1-47.2.0/forge-1.20.1-47.2.0-universal.zip")]
    public void RelativePath_IsBuiltFromCoordinates(string name, string expected)
    {
        Assert.Equal(expected, MavenCoordinates.Parse(name).RelativePath);
    }

    [Fact]
    public void Parse_Invalid_Throws()
    {
        Assert.Throws<FormatException>(() => MavenCoordinates.Parse("only:two"));
    }
}