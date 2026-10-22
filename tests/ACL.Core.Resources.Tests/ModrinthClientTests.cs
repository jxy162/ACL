using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Resources;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Resources.Tests;

public class ModrinthClientTests
{
    private static readonly Uri BaseUri = new("https://stub/modrinth/v2/");

    private const string SearchJson = """
    {
      "hits": [
        {
          "project_id": "AABBCC",
          "slug": "sodium",
          "title": "Sodium",
          "description": "Rendering engine",
          "icon_url": "https://cdn/icon.png",
          "downloads": 123456,
          "author": "jellysquid3",
          "versions": ["1.20.1", "1.20.2"],
          "categories": ["fabric", "optimization"],
          "project_type": "mod"
        }
      ],
      "offset": 0,
      "limit": 20,
      "total_hits": 1
    }
    """;

    private const string VersionsJson = """
    [
      {
        "name": "Sodium 0.5.3",
        "version_number": "mc1.20.1-0.5.3",
        "version_type": "release",
        "game_versions": ["1.20.1"],
        "loaders": ["fabric"],
        "files": [
          {
            "url": "https://cdn.modrinth.com/data/AABBCC/versions/xyz/sodium.jar",
            "filename": "sodium-0.5.3.jar",
            "size": 900,
            "hashes": { "sha1": "aa11", "sha512": "bb22" }
          }
        ]
      }
    ]
    """;

    [Fact]
    public async Task Search_ParsesHitsAndBuildsFacets()
    {
        StubHttpTransport http = new StubHttpTransport().OnPrefixJson("https://stub/modrinth/v2/search", SearchJson);
        ModrinthClient client = new(http, BaseUri);

        Result<ResourceSearchResult> result = await client.SearchAsync(
            new ResourceSearchQuery("sodium", ResourceKind.Mod, "1.20.1", ModLoaderKind.Fabric));

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        ResourceProject project = Assert.Single(result.Value!.Projects);
        Assert.Equal("AABBCC", project.Id);
        Assert.Equal("Sodium", project.Title);
        Assert.Equal(123456, project.Downloads);
        Assert.Equal(ResourceKind.Mod, project.Kind);
        Assert.Contains(ModLoaderKind.Fabric, project.Loaders);
        Assert.Equal(1, result.Value!.Total);

        string requested = http.LastRequest!.Uri.ToString();
        Assert.Contains("facets=", requested, StringComparison.Ordinal);
        Assert.Contains("project_type", Uri.UnescapeDataString(requested), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFiles_ParsesPrimaryFileHashes()
    {
        StubHttpTransport http = new StubHttpTransport().OnPrefixJson("https://stub/modrinth/v2/project/sodium/version", VersionsJson);
        ModrinthClient client = new(http, BaseUri);

        Result<IReadOnlyList<ResourceFile>> result = await client.GetFilesAsync("sodium", "1.20.1", ModLoaderKind.Fabric);

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        ResourceFile file = Assert.Single(result.Value!);
        Assert.Equal("sodium-0.5.3.jar", file.FileName);
        Assert.Equal("aa11", file.Sha1);
        Assert.Equal("bb22", file.Sha512);
        Assert.Equal(HashKind.Sha512, file.PreferredHash!.Kind);
        Assert.Contains(ModLoaderKind.Fabric, file.Loaders);
    }

    [Fact]
    public async Task Search_RateLimited_ReturnsRateLimitedCode()
    {
        StubHttpTransport http = new StubHttpTransport().OnPrefix("https://stub/modrinth/v2/search", _ => StubResponses.Status(429));
        ModrinthClient client = new(http, BaseUri);

        Result<ResourceSearchResult> result = await client.SearchAsync(new ResourceSearchQuery("x"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.RateLimited, result.Code);
    }
}