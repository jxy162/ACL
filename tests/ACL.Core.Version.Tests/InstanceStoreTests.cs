using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Version.Tests;

public class InstanceStoreTests
{
    private const string ManifestPath = "D:/app/instances/1.20.1-forge.json";

    [Fact]
    public void Manifest_RoundTrips()
    {
        InMemoryFileSystem fs = new();
        InstanceStore store = new(fs);
        DateTimeOffset now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        InstallManifest manifest = new(
            InstallManifest.CurrentSchemaVersion,
            new ComponentSet(
                new Dictionary<ModLoaderKind, string> { [ModLoaderKind.Forge] = "47.2.0" },
                new OptiFineRef("C8")),
            now,
            [
                new ManagedFile("libraries/net/minecraftforge/forge/1.20.1-47.2.0/forge.jar", "abc", now, "forge"),
                new ManagedFile("versions/1.20.1/1.20.1.jar", "def", now),
            ]);

        Result save = store.SaveManifest(ManifestPath, manifest);
        Assert.True(save.IsSuccess, save.Code + ": " + save.Detail);

        Result<InstallManifest> loaded = store.LoadManifest(ManifestPath);
        Assert.True(loaded.IsSuccess, loaded.Code + ": " + loaded.Detail);
        Assert.True(loaded.Value!.Components.EquivalentTo(manifest.Components));
        Assert.Equal(manifest.ManagedFiles.Count, loaded.Value!.ManagedFiles.Count);
        Assert.Equal("abc", loaded.Value!.ManagedFiles[0].Sha1);
        Assert.Equal(now, loaded.Value!.LastModifiedUtc);
    }

    [Fact]
    public void LoadManifest_MissingFile_ReturnsNotFound()
    {
        Result<InstallManifest> loaded = new InstanceStore(new InMemoryFileSystem()).LoadManifest("D:/none/x.json");

        Assert.False(loaded.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, loaded.Code);
    }

    [Fact]
    public void Descriptor_RoundTrips()
    {
        InMemoryFileSystem fs = new();
        InstanceStore store = new(fs);
        InstanceDescriptor descriptor = new(
            new InstanceId("1.20.1 Forge", "D:/mc/instances/forge"),
            McVersionSpec.FromName("1.20.1", DateTimeOffset.Parse("2024-06-13T00:00:00Z")),
            new ComponentSet(new Dictionary<ModLoaderKind, string> { [ModLoaderKind.Forge] = "47.2.0" }),
            "D:/mc/instances/forge/1.20.1-forge.json");

        Result save = store.SaveDescriptor("D:/app/instances/forge.json", descriptor);
        Assert.True(save.IsSuccess);

        Result<InstanceDescriptor> loaded = store.LoadDescriptor("D:/app/instances/forge.json");
        Assert.True(loaded.IsSuccess, loaded.Code + ": " + loaded.Detail);
        Assert.Equal("1.20.1 Forge", loaded.Value!.Id.Name);
        Assert.Equal("1.20.1", loaded.Value!.Version.Vanilla.Raw);
        Assert.Equal(200, loaded.Value!.Version.Drop);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsConfigInvalid()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile(ManifestPath, "{ not json");

        Result<InstallManifest> loaded = new InstanceStore(fs).LoadManifest(ManifestPath);

        Assert.False(loaded.IsSuccess);
        Assert.Equal(ErrorCode.ConfigInvalid, loaded.Code);
    }
}