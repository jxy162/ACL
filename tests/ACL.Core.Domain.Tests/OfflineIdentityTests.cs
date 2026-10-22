using ACL.Core.Domain;
using Xunit;

namespace ACL.Core.Domain.Tests;

public class OfflineIdentityTests
{
    [Fact]
    public void CreateUuid_IsDeterministic()
    {
        Assert.Equal(OfflineIdentity.CreateUuid("Steve"), OfflineIdentity.CreateUuid("Steve"));
        Assert.NotEqual(OfflineIdentity.CreateUuid("Steve"), OfflineIdentity.CreateUuid("Alex"));
    }

    [Fact]
    public void CreateUuid_SetsVersionAndVariantBits()
    {
        string uuid = OfflineIdentity.CreateUuid("Steve");

        Assert.Equal(32, uuid.Length);
        Assert.Equal('3', uuid[12]);                       // 版本 3
        Assert.Contains(uuid[16], "89ab");                 // RFC 4122 变体
        Assert.Equal(uuid, uuid.ToLowerInvariant());
    }

    [Fact]
    public void CreateSession_IsOfflineWithToken()
    {
        AuthSession session = OfflineIdentity.CreateSession("Steve");

        Assert.Equal(AccountType.Offline, session.Type);
        Assert.Equal("Steve", session.Name);
        Assert.Equal(OfflineIdentity.CreateUuid("Steve"), session.Uuid);
        Assert.False(string.IsNullOrEmpty(session.AccessToken));
    }

    [Fact]
    public void CreateUuid_RejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => OfflineIdentity.CreateUuid("  "));
    }
}