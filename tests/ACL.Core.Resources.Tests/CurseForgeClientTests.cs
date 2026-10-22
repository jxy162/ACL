using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Resources;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Resources.Tests;

public class CurseForgeClientTests
{
    private static readonly Uri BaseUri = new("https://stub/cf/v1/");

    private const string SearchJson = """
    {
      "data": [
        {
          "id": 238222,
          "name": "Just Enough Items",
          "slug": "jei",
          "summary": "View Items and Recipes",
          "downloadCount": 999999,
          "classId": 6,
          "logo": { "thumbnailUrl": "https://media/jei.png" },
          "authors": [ { "name": "mezz" } ],
          "latestFiles": [
            { "id": 456789, "fileName": "jei-1.20.1.jar", "fileLength": 1234, "gameVersions": ["1.20.1"], "modLoaderType": 1 }
          ]
        }
      ],
      "pagination": { "index": 0, "pageSize": 20, "totalCount": 1 }
    }
    """;

    private const string FilesJson = """
    {
      "data": [
        {
          "id": 456789,
          "fileName": "jei-1.20.1.jar",
          "downloadUrl": null,
          "fileLength": 1234,
          "gameVersions": ["1.20.1"],
          "modLoaderType": 1,
          "hashes": [ { "algo": 1, "value": "cc33" }, { "algo": 2, "value": "dd44" } ]
        }
      ]
    }
    """;

    private static StubSecretProvider Secrets() => new("test-key");

    [Fact]
    public async Task Search_ParsesDataAndSendsApiKey()
    {
        StubHttpTransport http = new StubHttpTransport().OnPrefixJson("https://stub/cf/v1/mods/search", SearchJson);
        CurseForgeClient client = new(http, Secrets(), BaseUri);

        Result<ResourceSearchResult> result = await client.SearchAsync(
            new ResourceSearchQuery("jei", ResourceKind.Mod, "1.20.1", ModLoaderKind.Forge));

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        ResourceProject project = Assert.Single(result.Value!.Projects);
        Assert.Equal("238222", project.Id);
        Assert.Equal("Just Enough Items", project.Title);
        Assert.Equal("mezz", project.Author);
        Assert.Contains(ModLoaderKind.Forge, project.Loaders);
        Assert.Equal(1, result.Value!.Total);
        Assert.Equal("test-key", http.LastRequest!.Headers!["x-api-key"]);
    }

    [Fact]
    public async Task GetFiles_BuildsForgeCdnUrl_WhenDownloadUrlMissing()
    {
        StubHttpTransport http = new StubHttpTransport().OnPrefixJson("https://stub/cf/v1/mods/238222/files", FilesJson);
        CurseForgeClient client = new(http, Secrets(), BaseUri);

        Result<IReadOnlyList<ResourceFile>> result = await client.GetFilesAsync("238222");

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        ResourceFile file = Assert.Single(result.Value!);
        Assert.Equal("https://edge.forgecdn.net/files/456/789/jei-1.20.1.jar", file.Url.ToString());
        Assert.Equal("cc33", file.Sha1);
        Assert.Equal("dd44", file.Sha512);
    }

    [Fact]
    public async Task MissingApiKey_ReturnsConfigInvalid()
    {
        CurseForgeClient client = new(new StubHttpTransport(), new StubSecretProvider(null), BaseUri);

        Result<ResourceSearchResult> result = await client.SearchAsync(new ResourceSearchQuery("x"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ConfigInvalid, result.Code);
    }

    [Fact]
    public async Task Forbidden_ReturnsAuthDenied()
    {
        StubHttpTransport http = new StubHttpTransport().OnPrefix("https://stub/cf/v1/mods/search", _ => StubResponses.Status(403));
        CurseForgeClient client = new(http, Secrets(), BaseUri);

        Result<ResourceSearchResult> result = await client.SearchAsync(new ResourceSearchQuery("x"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.AuthDenied, result.Code);
    }
}