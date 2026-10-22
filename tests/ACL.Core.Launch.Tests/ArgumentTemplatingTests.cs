using ACL.Core.Launch;
using Xunit;

namespace ACL.Core.Launch.Tests;

public class ArgumentTemplatingTests
{
    private static readonly Dictionary<string, string> Vars = new(StringComparer.Ordinal)
    {
        ["auth_player_name"] = "Steve",
        ["classpath"] = "a.jar",
    };

    [Fact]
    public void Expand_ReplacesKnownPlaceholders()
    {
        Assert.Equal("--username Steve", ArgumentTemplating.Expand("--username ${auth_player_name}", Vars));
        Assert.Equal("token Steve a.jar", ArgumentTemplating.Expand("token ${auth_player_name} ${classpath}", Vars));
    }

    [Fact]
    public void Expand_LeavesUnknownPlaceholderUntouched()
    {
        Assert.Equal("--x ${missing}", ArgumentTemplating.Expand("--x ${missing}", Vars));
    }

    [Fact]
    public void DistinctPreservingOrder_DropsDuplicateFlagAndItsValue()
    {
        List<string> input = ["-cp", "a.jar", "-Dx=1", "-Dx=1", "-cp", "b.jar", "-Dx=1"];

        List<string> result = ArgumentTemplating.DistinctPreservingOrder(input);

        Assert.Equal(["-cp", "a.jar", "-Dx=1"], result);
    }

    [Fact]
    public void DistinctPreservingOrder_KeepsPositionalArguments()
    {
        List<string> result = ArgumentTemplating.DistinctPreservingOrder(["--username", "Steve", "extra", "extra"]);

        Assert.Equal(["--username", "Steve", "extra", "extra"], result);
    }
}