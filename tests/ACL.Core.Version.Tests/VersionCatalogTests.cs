using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Version.Tests;

public class VersionCatalogTests
{
    private static readonly string Official = VersionCatalog.DefaultManifestUri.ToString();
    private const string Bmclapi = "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json";

    [Fact]
    public async Task GetManifestAsync_ReadsOfficialSource()
    {
        StubHttpTransport http = new StubHttpTransport().OnText(Official, VersionManifestParserTests.SampleManifest);
        VersionCatalog catalog = new(http);

        Result<VersionManifest> result = await catalog.GetManifestAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("1.21.5", result.Value!.Latest.Release);
        Assert.True(http.RequestCount >= 1);   // 并发取先：候选源可能都被请求
    }

    [Fact]
    public async Task GetManifestAsync_FallsBackToMirrorWhenOfficialFails()
    {
        StubHttpTransport http = new StubHttpTransport()
            .OnFail(Official)
            .OnText(Bmclapi, VersionManifestParserTests.SampleManifest);
        VersionCatalog catalog = new(http);

        Result<VersionManifest> result = await catalog.GetManifestAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, http.RequestCount);
    }

    [Fact]
    public async Task GetManifestAsync_AllSourcesFail_ReturnsFailure()
    {
        StubHttpTransport http = new StubHttpTransport().OnFail(Official).OnFail(Bmclapi);
        VersionCatalog catalog = new(http);

        Result<VersionManifest> result = await catalog.GetManifestAsync();

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetManifestAsync_OfficialServesInvalidJson_Fails()
    {
        StubHttpTransport http = new StubHttpTransport().OnText(Official, "<html>error</html>");
        VersionCatalog catalog = new(http);

        Result<VersionManifest> result = await catalog.GetManifestAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.VersionParseFailed, result.Code);
    }
}