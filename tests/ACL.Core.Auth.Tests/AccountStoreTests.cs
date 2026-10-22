using ACL.Core.Abstractions;
using ACL.Core.Auth;
using ACL.Core.Domain;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Auth.Tests;

public class AccountStoreTests
{
    private const string Path = "C:/data/ACL.accounts.json";

    private static AccountStore Create(InMemoryFileSystem fs)
        => new(fs, new FileTokenStore(fs, new TestSecretProtector(), "C:/data/accounts"), Path);

    [Fact]
    public void List_EmptyWhenNoFile()
    {
        Result<IReadOnlyList<AccountProfile>> result = Create(new InMemoryFileSystem()).List();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void Save_ThenList_ReturnsAccountsMostRecentFirst()
    {
        InMemoryFileSystem fs = new InMemoryFileSystem();
        AccountStore store = Create(fs);
        DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        store.Save(AccountProfile.Offline("Steve", now));
        store.Save(new AccountProfile("uuid-1", AccountType.Microsoft, "Alex", "uuid-1", now.AddHours(2)));

        Result<IReadOnlyList<AccountProfile>> listed = store.List();
        Assert.True(listed.IsSuccess);
        Assert.Equal(2, listed.Value!.Count);
        Assert.Equal("Alex", listed.Value[0].Name);
        Assert.Equal("正版", listed.Value[0].TypeLabel);
        Assert.Equal("离线", listed.Value[1].TypeLabel);
    }

    [Fact]
    public void Save_WithSession_PersistsEncryptedSession()
    {
        InMemoryFileSystem fs = new InMemoryFileSystem();
        AccountStore store = Create(fs);
        AuthSession session = new(AccountType.Microsoft, "uuid-1", "Alex", "access-token", "refresh-token");

        store.Save(new AccountProfile("uuid-1", AccountType.Microsoft, "Alex", "uuid-1", DateTimeOffset.UnixEpoch), session);

        Result<AuthSession> loaded = store.LoadSession("uuid-1");
        Assert.True(loaded.IsSuccess);
        Assert.Equal("access-token", loaded.Value!.AccessToken);
        Assert.Equal("refresh-token", loaded.Value.RefreshToken);

        // 明文令牌绝不能出现在落盘文本里。
        string raw = System.Text.Encoding.UTF8.GetString(fs.ReadAllBytes("C:/data/accounts/uuid-1.json"));
        Assert.DoesNotContain("access-token", raw, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_UpdateExisting_DoesNotDuplicate()
    {
        InMemoryFileSystem fs = new InMemoryFileSystem();
        AccountStore store = Create(fs);

        store.Save(AccountProfile.Offline("Steve", DateTimeOffset.UnixEpoch));
        store.Save(new AccountProfile("steve", AccountType.Offline, "Steve2", null, DateTimeOffset.UnixEpoch.AddDays(1)));

        Result<IReadOnlyList<AccountProfile>> listed = store.List();
        Assert.Single(listed.Value!);
        Assert.Equal("Steve2", listed.Value![0].Name);
    }

    [Fact]
    public void Delete_RemovesProfileAndSession()
    {
        InMemoryFileSystem fs = new InMemoryFileSystem();
        AccountStore store = Create(fs);
        store.Save(new AccountProfile("uuid-1", AccountType.Microsoft, "Alex", "uuid-1", DateTimeOffset.UnixEpoch), new AuthSession(AccountType.Microsoft, "uuid-1", "Alex", "a", "r"));

        Result deleted = store.Delete("uuid-1");

        Assert.True(deleted.IsSuccess);
        Assert.Empty(store.List().Value!);
        Assert.False(store.LoadSession("uuid-1").IsSuccess);
    }

    [Fact]
    public void OfflineProfile_DerivesStableUuidAndLetter()
    {
        AccountProfile profile = AccountProfile.Offline("Steve", DateTimeOffset.UnixEpoch);

        Assert.Equal("steve", profile.Id);
        Assert.Equal(OfflineIdentity.CreateUuid("Steve"), profile.Uuid);
        Assert.Equal("S", profile.AvatarLetter);
    }
}