using ACL.Core.Abstractions;
using ACL.Infrastructure;
using ACL.Testing;
using Xunit;

namespace ACL.Integration.Tests;

public class SecretProviderTests
{
    private sealed class MapProvider : ISecretProvider
    {
        private readonly Dictionary<string, string> _values;

        public MapProvider(Dictionary<string, string> values) => _values = values;

        public string? GetSecret(string key) => _values.TryGetValue(key, out string? value) ? value : null;
    }

    [Fact]
    public void Composite_ReturnsFirstNonEmpty()
    {
        CompositeSecretProvider provider = new(
            new MapProvider(new() { ["K"] = string.Empty }),
            new MapProvider(new() { ["K"] = "second" }),
            new MapProvider(new() { ["K"] = "third" }));

        Assert.Equal("second", provider.GetSecret("K"));
        Assert.Null(provider.GetSecret("missing"));
    }

    [Fact]
    public void PrefixedCompat_MapsAclToPcl()
    {
        PrefixedCompatSecretProvider provider = new(
            new MapProvider(new() { ["PCL_MS_CLIENT_ID"] = "legacy-id" }));

        Assert.Equal("legacy-id", provider.GetSecret("ACL_MS_CLIENT_ID"));
        Assert.Null(provider.GetSecret("OTHER_KEY"));
    }

    [Fact]
    public void File_ReadsPortableSecrets()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile("D:/acl/ACL.secrets.json", """{ "ACL_CURSEFORGE_API_KEY": "abc", "ACL_MS_CLIENT_ID": "id" }""");
        FileSecretProvider provider = new(fs, "D:/acl/ACL.secrets.json");

        Assert.Equal("abc", provider.GetSecret("ACL_CURSEFORGE_API_KEY"));
        Assert.Equal("id", provider.GetSecret("ACL_MS_CLIENT_ID"));
        Assert.Null(provider.GetSecret("ACL_MISSING"));
    }

    [Fact]
    public void File_MissingFile_ReturnsNull()
    {
        FileSecretProvider provider = new(new InMemoryFileSystem(), "D:/acl/absent.json");

        Assert.Null(provider.GetSecret("ACL_MS_CLIENT_ID"));
    }
}