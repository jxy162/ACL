using ACL.Core.Domain;
using Xunit;

namespace ACL.Core.Domain.Tests;

public class VersionParsingTests
{
    [Theory]
    [InlineData("1.20.1", 20, 0, 1, 200)]   // 旧格式：去掉前导 1，次版本上移
    [InlineData("1.21.5", 21, 0, 5, 210)]
    [InlineData("1.14", 14, 0, 0, 140)]
    [InlineData("26.3.2", 26, 3, 2, 263)]   // 新格式：年份版本号保持原样
    [InlineData("26.1", 26, 1, 0, 261)]
    [InlineData("26.1-snapshot-1", 26, 1, 0, 261)]
    public void Parse_DottedVersions_ProduceComparableAndDrop(
        string name, int major, int minor, int build, int drop)
    {
        VersionNumbers result = VersionParsing.Parse(name);
        Assert.True(result.HasNumbers);
        Assert.Equal(new Version(major, minor, build), result.Comparable);
        Assert.Equal(drop, result.Drop);
    }

    [Fact]
    public void Parse_ModernVersion_IsNotShifted()
    {
        VersionNumbers result = VersionParsing.Parse("26.3.2");
        Assert.Equal(263, result.Drop);
        Assert.True(result.Comparable >= VersionParsing.Parse("26.1").Comparable);
    }

    [Theory]
    [InlineData("16w01a")]   // 旧式快照：无版本号
    [InlineData("未知版本")]
    public void Parse_WithoutReliableNumbers_FallsBack(string name)
    {
        VersionNumbers result = VersionParsing.Parse(name);
        Assert.False(result.HasNumbers);
        Assert.Equal(VersionParsing.UnknownComparable, result.Comparable);
        Assert.Equal(VersionParsing.UnknownDrop, result.Drop);
    }

    [Fact]
    public void Parse_LegacySnapshot_FlaggedAsSnapshot()
    {
        Assert.True(VersionParsing.Parse("16w01a").IsSnapshot);
        Assert.True(VersionParsing.Parse("26.1-snapshot-1").IsSnapshot);
        Assert.False(VersionParsing.Parse("1.20.1").IsSnapshot);
    }

    [Fact]
    public void Parse_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => VersionParsing.Parse(null!));
    }
}