using ACL.Core.Domain;
using ACL.Core.Java;
using Xunit;

namespace ACL.Core.Java.Tests;

public class JavaSelectorTests
{
    private static readonly JavaRuntime J8 = new("C:/java8", new Version(8, 0, 392), true);
    private static readonly JavaRuntime J17 = new("C:/java17", new Version(17, 0, 9), true);
    private static readonly JavaRuntime J21 = new("C:/java21", new Version(21, 0, 2), true);
    private static readonly JavaRuntime J17x86 = new("C:/java17x86", new Version(17, 0, 9), false);

    [Fact]
    public void Select_PrefersHighestVersionInRange()
    {
        JavaSelector selector = new();
        JavaRequirement requirement = new(new JavaVersionRange(new Version(17, 0, 0)));

        JavaRuntime? selected = selector.Select([J8, J17, J21], requirement);

        Assert.Equal(J21, selected);
    }

    [Fact]
    public void Select_Skips32Bit()
    {
        JavaSelector selector = new();
        JavaRequirement requirement = new(new JavaVersionRange(new Version(17, 0, 0)));

        JavaRuntime? selected = selector.Select([J17x86], requirement);

        Assert.Null(selected);
    }

    [Fact]
    public void Select_RespectsUpperBound()
    {
        JavaSelector selector = new();
        JavaRequirement requirement = new(new JavaVersionRange(new Version(8, 0, 0), new Version(17, 0, 0)));

        JavaRuntime? selected = selector.Select([J8, J17, J21], requirement);

        Assert.Equal(J8, selected);
    }

    [Fact]
    public void Select_NoMatch_ReturnsNull()
    {
        JavaSelector selector = new();
        JavaRequirement requirement = new(new JavaVersionRange(new Version(25, 0, 0)));

        Assert.Null(selector.Select([J8, J17], requirement));
    }
}
