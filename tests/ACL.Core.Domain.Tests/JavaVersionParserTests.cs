using ACL.Core.Domain;
using Xunit;

namespace ACL.Core.Domain.Tests;

public class JavaVersionParserTests
{
    [Theory]
    [InlineData("17.0.9", 17, 0, 9)]
    [InlineData("21", 21, 0, 0)]
    [InlineData("11.0.20+8", 11, 0, 20)]
    [InlineData("\"17.0.9\"", 17, 0, 9)]
    [InlineData("1.8.0_392", 8, 0, 392)]
    [InlineData("1.7.0_80", 7, 0, 80)]
    public void Parse_HandlesCommonFormats(string text, int major, int minor, int build)
    {
        Assert.Equal(new Version(major, minor, build), JavaVersionParser.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("   ")]
    public void TryParse_Invalid_ReturnsFalse(string text)
    {
        Assert.False(JavaVersionParser.TryParse(text, out _));
    }

    [Fact]
    public void Parse_Invalid_Throws()
    {
        Assert.Throws<FormatException>(() => JavaVersionParser.Parse("nope"));
    }
}