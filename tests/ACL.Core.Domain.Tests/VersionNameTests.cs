using ACL.Core.Domain;
using Xunit;

namespace ACL.Core.Domain.Tests;

public class VersionNameTests
{
    [Theory]
    [InlineData("1.13.2", "1.13.1")]      // 补丁号更大者更新
    [InlineData("1.20", "1.7.10")]        // 主次版本更大者更新
    [InlineData("1.20.1", "1.20")]
    [InlineData("1.14 Pre-Release 2", "1.14 Pre-Release 1")]
    [InlineData("1.21.5", "1.21")]
    [InlineData("26.2", "26.1")]
    public void Compare_LeftNewer_ReturnsPositive(string left, string right)
    {
        Assert.True(VersionName.Compare(left, right) > 0, $"{left} 应新于 {right}。");
        Assert.True(VersionName.Compare(right, left) < 0, $"{right} 应旧于 {left}。");
    }

    [Theory]
    [InlineData("1.7.10-pre4", "1.7.10")]              // 预发布旧于正式版
    [InlineData("1.8_pre", "1.8")]
    [InlineData("1.14 Pre-Release 2", "1.14")]
    [InlineData("26.1-snapshot-1", "26.1")]
    [InlineData("未知版本", "1.20")]                     // 未知版本视为最旧
    public void Compare_LeftOlder_ReturnsNegative(string left, string right)
    {
        Assert.True(VersionName.Compare(left, right) < 0, $"{left} 应旧于 {right}。");
        Assert.True(VersionName.Compare(right, left) > 0, $"{right} 应新于 {left}。");
    }

    [Theory]
    [InlineData("1.13.2", "1.13.2")]
    [InlineData("1.20", "1.20")]
    [InlineData("26.1-snapshot-1", "26.1-snapshot-1")]
    public void Compare_Same_ReturnsZero(string left, string right)
    {
        Assert.Equal(0, VersionName.Compare(left, right));
    }

    [Fact]
    public void Compare_ChineseAliases_BehaveLikeEnglish()
    {
        Assert.Equal(0, VersionName.Compare("1.14 快照 1", "1.14 snapshot 1"));
        Assert.Equal(0, VersionName.Compare("1.14 预览版 1", "1.14 pre 1"));
    }

    [Fact]
    public void Instance_ImplementsComparableAndIsAtLeast()
    {
        VersionName newer = new("1.20.1");
        VersionName older = new("1.20");
        Assert.True(newer.CompareTo(older) > 0);
        Assert.True(newer.IsAtLeast(older));
        Assert.False(older.IsAtLeast(newer));
    }

    [Fact]
    public void Ctor_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new VersionName(null!));
    }
}