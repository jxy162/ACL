using ACL.Core.Domain;
using Xunit;

namespace ACL.Core.Domain.Tests;

public class GameExitReasonTests
{
    [Theory]
    [InlineData(0, GameExitReason.NormalExit)]
    [InlineData(1, GameExitReason.ModError)]
    [InlineData(-1073741819, GameExitReason.GraphicsDriverError)]
    [InlineData(-1073740791, GameExitReason.NativeCrash)]
    [InlineData(-1, GameExitReason.UnknownError)]
    [InlineData(12345, GameExitReason.UnknownError)]
    public void FromExitCode_MapsKnownCodes(int exitCode, GameExitReason expected)
    {
        Assert.Equal(expected, GameExitReasons.FromExitCode(exitCode));
    }

    [Fact]
    public void FromLogAndExitCode_OutOfMemoryOverridesExitCode()
    {
        string log = "Exception in thread \"main\" java.lang.OutOfMemoryError: Java heap space";

        Assert.Equal(GameExitReason.MemoryError, GameExitReasons.FromLogAndExitCode(log, 1));
    }

    [Fact]
    public void FromLogAndExitCode_ClassVersionMismatchIsJavaError()
    {
        Assert.Equal(GameExitReason.JavaError, GameExitReasons.FromLogAndExitCode("java.lang.UnsupportedClassVersionError", 1));
    }

    [Fact]
    public void FromLogAndExitCode_NoSignal_FallsBackToExitCode()
    {
        Assert.Equal(GameExitReason.NormalExit, GameExitReasons.FromLogAndExitCode("done", 0));
    }
}