using ACL.Platform.Windows;
using Xunit;

namespace ACL.Platform.Windows.Tests;

public class DpapiSecretProtectorTests
{
    [Fact]
    public void ProtectThenUnprotect_RoundTrips()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        DpapiSecretProtector protector = new();

        var protectedResult = protector.Protect("secret-token");
        Assert.True(protectedResult.IsSuccess, protectedResult.Code + ": " + protectedResult.Detail);
        Assert.NotEqual("secret-token", protectedResult.Value);

        var plain = protector.Unprotect(protectedResult.Value!);
        Assert.True(plain.IsSuccess, plain.Code + ": " + plain.Detail);
        Assert.Equal("secret-token", plain.Value);
    }

    [Fact]
    public void Unprotect_InvalidInput_Fails()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.False(new DpapiSecretProtector().Unprotect("not-base64!!").IsSuccess);
    }
}