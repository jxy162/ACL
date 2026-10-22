using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Infrastructure;
using ACL.Testing;
using Xunit;

namespace ACL.Integration.Tests;

public class PortableSettingsStoreTests
{
    private const string Path = "D:/acl/ACL.config.json";

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        Result<AclSettings> loaded = new PortableSettingsStore(new InMemoryFileSystem(), Path).Load();

        Assert.True(loaded.IsSuccess);
        Assert.Equal(512, loaded.Value!.MinMemoryMb);
        Assert.Equal("Steve", loaded.Value!.PlayerName);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        InMemoryFileSystem fs = new();
        PortableSettingsStore store = new(fs, Path);
        AclSettings settings = new()
        {
            MinMemoryMb = 1024,
            MaxMemoryMb = 8192,
            PreferOfficialMirror = false,
            PlayerName = "Alex",
            SelectedVersionId = "1.20.1",
        };

        Assert.True(store.Save(settings).IsSuccess);

        Result<AclSettings> loaded = store.Load();
        Assert.True(loaded.IsSuccess, loaded.Code + ": " + loaded.Detail);
        Assert.Equal(1024, loaded.Value!.MinMemoryMb);
        Assert.Equal(8192, loaded.Value!.MaxMemoryMb);
        Assert.False(loaded.Value!.PreferOfficialMirror);
        Assert.Equal("Alex", loaded.Value!.PlayerName);
        Assert.Equal("1.20.1", loaded.Value!.SelectedVersionId);
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToDefaults()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile(Path, "{ not json");

        Result<AclSettings> loaded = new PortableSettingsStore(fs, Path).Load();

        Assert.True(loaded.IsSuccess);
        Assert.Equal(512, loaded.Value!.MinMemoryMb);
    }
}