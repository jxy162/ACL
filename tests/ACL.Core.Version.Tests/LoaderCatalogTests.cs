using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version.Loaders;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Version.Tests;

public class LoaderCatalogTests
{
    private const string ForgeMeta = "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";
    private const string NeoForgeApi = "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge";
    private const string FabricMeta = "https://meta.fabricmc.net/v2/versions/loader/1.20.1";
    private const string OptiFineList = "https://bmclapi2.bangbang93.com/optifine/versionList";

    [Fact]
    public async Task GetForgeVersions_FiltersByGameVersionAndSortsDescending()
    {
        const string xml = """
        <metadata>
          <versioning>
            <versions>
              <version>1.20.1-47.2.0</version>
              <version>1.20.1-47.1.0</version>
              <version>1.19.2-43.2.0</version>
            </versions>
          </versioning>
        </metadata>
        """;
        StubHttpTransport http = new StubHttpTransport().OnText(ForgeMeta, xml);
        LoaderCatalog catalog = new(http);

        Result<IReadOnlyList<LoaderOffer>> result = await catalog.GetVersionsAsync(ModLoaderKind.Forge, "1.20.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("47.2.0", result.Value[0].Version);
        Assert.Equal("47.1.0", result.Value[1].Version);
        Assert.All(result.Value, offer => Assert.Equal("1.20.1", offer.GameVersion));
    }

    [Fact]
    public async Task GetNeoForgeVersions_MapsModernVersioning()
    {
        const string json = """{ "versions": ["21.1.72", "21.1.9", "20.4.237"] }""";
        StubHttpTransport http = new StubHttpTransport().OnText(NeoForgeApi, json);
        LoaderCatalog catalog = new(http);

        Result<IReadOnlyList<LoaderOffer>> result = await catalog.GetVersionsAsync(ModLoaderKind.NeoForge, "1.21.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(["21.1.72", "21.1.9"], result.Value!.Select(o => o.Version));
    }

    [Fact]
    public async Task GetFabricVersions_ReadsLoaderEntries()
    {
        const string json = """
        [
          { "loader": { "version": "0.15.11", "stable": true }, "intermediary": { "version": "1.20.1" } },
          { "loader": { "version": "0.15.10", "stable": false }, "intermediary": { "version": "1.20.1" } }
        ]
        """;
        StubHttpTransport http = new StubHttpTransport().OnText(FabricMeta, json);
        LoaderCatalog catalog = new(http);

        Result<IReadOnlyList<LoaderOffer>> result = await catalog.GetVersionsAsync(ModLoaderKind.Fabric, "1.20.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.True(result.Value[0].IsRecommended);
    }

    [Fact]
    public async Task GetOptiFineVersions_MatchesGameVersionAndBuildsUri()
    {
        const string json = """
        [
          { "mcversion": "1.20.1", "type": "HD_U", "patch": "I6", "filename": "OptiFine_1.20.1_HD_U_I6.jar" },
          { "mcversion": "1.19.2", "type": "HD_U", "patch": "H9", "filename": "OptiFine_1.19.2_HD_U_H9.jar" }
        ]
        """;
        StubHttpTransport http = new StubHttpTransport().OnText(OptiFineList, json);
        LoaderCatalog catalog = new(http);

        Result<IReadOnlyList<LoaderOffer>> result = await catalog.GetVersionsAsync(ModLoaderKind.OptiFine, "1.20.1");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("1.20.1_HD_U_I6", result.Value![0].Version);
        Assert.Equal("https://bmclapi2.bangbang93.com/optifine/1.20.1/HD_U/I6", result.Value![0].DownloadUri!.ToString());
    }

    [Fact]
    public async Task GetVersions_UnknownKind_ReturnsEmpty()
    {
        LoaderCatalog catalog = new(new StubHttpTransport());

        Result<IReadOnlyList<LoaderOffer>> result = await catalog.GetVersionsAsync(ModLoaderKind.None, "1.20.1");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetNeoForgeVersions_ReadsMirrorDirectoryListing()
    {
        const string mirror = "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge";
        const string json = """
        { "name": "forge", "files": [
            { "name": "1.20.1-47.1.9", "type": "DIRECTORY" },
            { "name": "1.20.1-47.1.11", "type": "DIRECTORY" },
            { "name": "1.19.4-45.1.0", "type": "DIRECTORY" }
        ] }
        """;
        StubHttpTransport http = new StubHttpTransport()
            .OnFail("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge")
            .OnText(mirror, json);
        LoaderCatalog catalog = new(http);

        Result<IReadOnlyList<LoaderOffer>> result = await catalog.GetVersionsAsync(ModLoaderKind.NeoForge, "1.20.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(["47.1.11", "47.1.9"], result.Value!.Select(o => o.Version));
    }

    [Fact]
    public async Task GetForgeVersions_SkipsUnparsableSourceAndKeepsOfficial()
    {
        const string mirror = "https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/maven-metadata.xml";
        const string xml = "<metadata><versioning><versions><version>1.20.1-47.2.0</version></versions></versioning></metadata>";
        StubHttpTransport http = new StubHttpTransport()
            .OnText(ForgeMeta, "<html>mirror error</html>")   // 官方源返回错误页
            .OnText(mirror, xml);                              // 镜像才是真的
        LoaderCatalog catalog = new(http);

        Result<IReadOnlyList<LoaderOffer>> result = await catalog.GetVersionsAsync(ModLoaderKind.Forge, "1.20.1");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("47.2.0", result.Value![0].Version);
    }

    [Theory]
    [InlineData("1.21.1", "21.1.")]
    [InlineData("1.21", "21.")]
    [InlineData("1", null)]
    public void NeoForgePrefix_DerivesFromGameVersion(string gameVersion, string? expected)
        => Assert.Equal(expected, LoaderCatalog.NeoForgePrefix(gameVersion));
}