using ACL.Core.Domain;
using ACL.Core.Modpack;
using ACL.Core.Resources;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Modpack.Tests;

public class CurseForgeFileResolverTests
{
    private const string FilesJson = """
    {
      "data": [
        { "id": 456789, "fileName": "jei-1.20.1.jar", "downloadUrl": "https://edge/jei.jar", "fileLength": 10, "hashes": [ { "algo": 1, "value": "aa" } ] },
        { "id": 111222, "fileName": "shaderpack-complementary.zip", "downloadUrl": "https://edge/shader.zip", "fileLength": 20, "hashes": [] }
      ]
    }
    """;

    [Fact]
    public async Task Resolve_MapsByIdAndChoosesFolder()
    {
        StubHttpTransport http = new StubHttpTransport().OnPrefixJson("https://stub/cf/v1/mods/files", FilesJson);
        CurseForgeClient client = new(http, new StubSecretProvider("key"), new Uri("https://stub/cf/v1/"));
        CurseForgeModpackFileResolver resolver = new(client);

        ModpackManifest manifest = new(
            "pack", null, "1.20.1", [],
            [
                new ModpackFile(string.Empty, [], null, null, 0, true, 238222, 456789),
                new ModpackFile(string.Empty, [], null, null, 0, true, 999, 111222),
            ],
            "overrides", ModpackFormat.CurseForge);

        var result = await resolver.ResolveAsync(manifest);

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("mods/jei-1.20.1.jar", result.Value![0].RelativePath);
        Assert.Equal("shaderpacks/shaderpack-complementary.zip", result.Value![1].RelativePath);
        Assert.Equal(new Uri("https://edge/jei.jar"), result.Value![0].Downloads[0]);
        Assert.Equal("aa", result.Value![0].Sha1);
    }

    [Fact]
    public async Task Resolve_NoUnresolved_ReturnsEmpty()
    {
        CurseForgeClient client = new(new StubHttpTransport(), new StubSecretProvider("key"));
        ModpackManifest manifest = new("pack", null, "1.20.1", [], [], "overrides", ModpackFormat.Modrinth);

        var result = await new CurseForgeModpackFileResolver(client).ResolveAsync(manifest);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }
}