using ACL.Core.Domain;
using Xunit;

namespace ACL.Core.Domain.Tests;

public class RuleEvaluatorTests
{
    [Fact]
    public void NoRules_IsAllowed()
    {
        Assert.True(RuleEvaluator.IsAllowed(null, OsSpec.WindowsX64));
        Assert.True(RuleEvaluator.IsAllowed([], OsSpec.LinuxX64));
    }

    [Fact]
    public void AllowWindows_IsAllowedOnWindowsOnly()
    {
        RuleEntry[] rules = [new("allow", "windows")];

        Assert.True(RuleEvaluator.IsAllowed(rules, OsSpec.WindowsX64));
        Assert.False(RuleEvaluator.IsAllowed(rules, OsSpec.LinuxX64));
    }

    [Fact]
    public void AllowThenDisallow_ExcludesLinuxOnly()
    {
        RuleEntry[] rules = [new("allow"), new("disallow", "linux")];

        Assert.True(RuleEvaluator.IsAllowed(rules, OsSpec.WindowsX64));
        Assert.False(RuleEvaluator.IsAllowed(rules, OsSpec.LinuxX64));
    }

    [Fact]
    public void LoneDisallow_DisallowsEverything()
    {
        // Minecraft 语义：默认不允许，未匹配到的规则不改变结果。
        RuleEntry[] rules = [new("disallow", "linux")];

        Assert.False(RuleEvaluator.IsAllowed(rules, OsSpec.WindowsX64));
        Assert.False(RuleEvaluator.IsAllowed(rules, OsSpec.LinuxX64));
    }

    [Fact]
    public void ArchSpecificRule_MatchesArch()
    {
        RuleEntry[] rules = [new("allow", "osx", null, "arm64")];

        Assert.True(RuleEvaluator.IsAllowed(rules, new OsSpec("osx", "arm64")));
        Assert.False(RuleEvaluator.IsAllowed(rules, OsSpec.MacOsX64));
    }

    [Fact]
    public void LastMatchingRuleWins()
    {
        RuleEntry[] rules = [new("allow", "windows"), new("disallow", "windows")];

        Assert.False(RuleEvaluator.IsAllowed(rules, OsSpec.WindowsX64));
    }
}