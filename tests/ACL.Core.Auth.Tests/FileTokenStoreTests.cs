using System.Text;
using ACL.Core.Abstractions;
using ACL.Core.Auth;
using ACL.Core.Domain;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Auth.Tests;

public class FileTokenStoreTests
{
    private const string Directory = "D:/app/secrets";

    [Fact]
    public void SaveThenLoad_RoundTripsAndProtectsTokens()
    {
        InMemoryFileSystem fs = new();
        FileTokenStore store = new(fs, new TestSecretProtector(), Directory);
        AuthSession session = new(
            AccountType.Microsoft, "uuid-1", "Notch", "mc-token", "refresh-1", DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        Assert.True(store.Save("acc-1", session).IsSuccess);

        string raw = Encoding.UTF8.GetString(fs.ReadAllBytes(Path.Combine(Directory, "acc-1.json")));
        Assert.DoesNotContain("mc-token", raw);
        Assert.DoesNotContain("refresh-1", raw);

        Result<AuthSession> loaded = store.Load("acc-1");
        Assert.True(loaded.IsSuccess, loaded.Code + ": " + loaded.Detail);
        Assert.Equal(session, loaded.Value);
    }

    [Fact]
    public void Load_Missing_ReturnsNotFound()
    {
        FileTokenStore store = new(new InMemoryFileSystem(), new TestSecretProtector(), Directory);

        Result<AuthSession> loaded = store.Load("nobody");

        Assert.False(loaded.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, loaded.Code);
    }

    [Fact]
    public void Delete_RemovesSession()
    {
        InMemoryFileSystem fs = new();
        FileTokenStore store = new(fs, new TestSecretProtector(), Directory);
        store.Save("acc-1", OfflineIdentity.CreateSession("Steve"));

        Assert.True(store.Delete("acc-1").IsSuccess);
        Assert.Equal(ErrorCode.NotFound, store.Load("acc-1").Code);
    }

    [Fact]
    public void Save_AccountIdIsSanitized_NoTraversalOutsideDirectory()
    {
        InMemoryFileSystem fs = new();
        FileTokenStore store = new(fs, new TestSecretProtector(), Directory);

        Assert.True(store.Save("../../evil", OfflineIdentity.CreateSession("Steve")).IsSuccess);

        foreach (string path in fs.EnumerateFiles(Directory))
        {
            Assert.StartsWith(Directory, path, StringComparison.Ordinal);
        }
    }
}