using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Download;
using ACL.Core.Version;
using ACL.Core.Version.Loaders;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Version.Tests;

public class LoaderInstallerTests
{
    private const string Manifest = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";
    private const string VanillaJson = "https://piston-meta.mojang.com/v1/packages/aaa/1.20.1.json";
    private const string ClientJar = "https://piston-meta.mojang.com/v1/packages/aaa/client.jar";
    private const string FabricProfile = "https://meta.fabricmc.net/v2/versions/loader/1.20.1/0.15.11/profile/json";
    private const string FabricLib = "https://maven.fabricmc.net/net/fabricmc/fabric-loader/0.15.11/fabric-loader-0.15.11.jar";
    private const string ForgeMeta = "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";

    private const string ManifestJson = """
    {
      "latest": { "release": "1.20.1", "snapshot": "23w31a" },
      "versions": [
        {
          "id": "1.20.1", "type": "release",
          "url": "https://piston-meta.mojang.com/v1/packages/aaa/1.20.1.json",
          "time": "2023-06-12T12:00:00+00:00", "releaseTime": "2023-06-12T11:00:00+00:00",
          "sha1": "aaaa", "complianceLevel": 1
        }
      ]
    }
    """;

    private const string VanillaVersionJson = """
    {
      "id": "1.20.1",
      "mainClass": "net.minecraft.client.main.Main",
      "type": "release",
      "downloads": { "client": { "url": "https://piston-meta.mojang.com/v1/packages/aaa/client.jar", "size": 8 } },
      "libraries": [],
      "arguments": { "game": [], "jvm": [] }
    }
    """;

    private const string FabricProfileJson = """
    {
      "id": "fabric-loader-0.15.11-1.20.1",
      "inheritsFrom": "1.20.1",
      "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
      "libraries": [
        { "name": "net.fabricmc:fabric-loader:0.15.11", "url": "https://maven.fabricmc.net/" }
      ]
    }
    """;

    private static LoaderInstaller CreateInstaller(StubHttpTransport http, InMemoryFileSystem fs, IProcessLauncher process)
    {
        DownloadService download = new(http, fs, null, null, null, DownloadThrottle.Unlimited);
        return new LoaderInstaller(new VersionCatalog(http), http, fs, download, process, new LoaderCatalog(http));
    }

    private static StubHttpTransport BaseHttp()
        => new StubHttpTransport()
            .OnText(Manifest, ManifestJson)
            .OnText(VanillaJson, VanillaVersionJson)
            .OnText(FabricProfile, FabricProfileJson)
            .OnContent(ClientJar, TestData.MakeBytes(8))
            .OnContent(FabricLib, TestData.MakeBytes(16));

    [Fact]
    public async Task Install_Vanilla_WritesVersionJsonAndClientJar()
    {
        InMemoryFileSystem fs = new InMemoryFileSystem();
        LoaderInstaller installer = CreateInstaller(BaseHttp(), fs, new StubProcessLauncher());

        Result<LoaderInstallResult> result = await installer.InstallAsync(new LoaderInstallRequest(
            ModLoaderKind.None, "1.20.1", null, "1.20.1", "C:/game"));

        Assert.True(result.IsSuccess);
        Assert.True(fs.FileExists("C:/game/versions/1.20.1/1.20.1.json"));
        Assert.True(fs.FileExists("C:/game/versions/1.20.1/1.20.1.jar"));
    }

    [Fact]
    public async Task Install_Fabric_WritesInstanceJsonAndLibraries()
    {
        InMemoryFileSystem fs = new InMemoryFileSystem();
        LoaderInstaller installer = CreateInstaller(BaseHttp(), fs, new StubProcessLauncher());

        Result<LoaderInstallResult> result = await installer.InstallAsync(new LoaderInstallRequest(
            ModLoaderKind.Fabric, "1.20.1", "0.15.11", "my-fabric", "C:/game"));

        Assert.True(result.IsSuccess);
        Assert.True(fs.FileExists("C:/game/versions/my-fabric/my-fabric.json"));
        Assert.True(fs.FileExists("C:/game/libraries/net/fabricmc/fabric-loader/0.15.11/fabric-loader-0.15.11.jar"));
        Assert.True(fs.FileExists("C:/game/versions/my-fabric/my-fabric.jar"));

        // 本地实例 JSON 的 id 必须与目录名一致（否则 inheritsFrom 解析会错乱）。
        Result<VersionJson> local = new VersionSource(new VersionCatalog(new StubHttpTransport()), new StubHttpTransport(), fs, "C:/game/versions")
            .TryLoadLocal("my-fabric");
        Assert.True(local.IsSuccess);
        Assert.Equal("my-fabric", local.Value!.Id);
        Assert.Equal("1.20.1", local.Value.InheritsFrom);
    }

    [Fact]
    public async Task Install_Forge_RunsOfficialInstallerAndDetectsInstance()
    {
        InMemoryFileSystem fs = new InMemoryFileSystem();
        const string installerJar = "https://maven.minecraftforge.net/net/minecraftforge/forge/1.20.1-47.2.0/forge-1.20.1-47.2.0-installer.jar";
        StubHttpTransport http = BaseHttp().OnContent(installerJar, TestData.MakeBytes(32));

        // 安装器进程桩：模拟官方安装器写出实例目录。
        StubProcessLauncher process = new StubProcessLauncher().On("C:/java/bin/java.exe", _ =>
        {
            fs.WriteFile("C:/game/versions/1.20.1-forge-47.2.0/1.20.1-forge-47.2.0.json", """
            { "id": "1.20.1-forge-47.2.0", "inheritsFrom": "1.20.1", "libraries": [ { "name": "net.minecraftforge:forge:1.20.1-47.2.0" } ] }
            """);
            return Result<ProcessResult>.Success(new ProcessResult(0, "done", string.Empty));
        });

        LoaderInstaller installer = CreateInstaller(http, fs, process);

        Result<LoaderInstallResult> result = await installer.InstallAsync(new LoaderInstallRequest(
            ModLoaderKind.Forge, "1.20.1", "47.2.0", "1.20.1-forge-47.2.0", "C:/game", "C:/java/bin/java.exe"));

        Assert.True(result.IsSuccess);
        Assert.Contains("1.20.1-forge-47.2.0", result.Value!.CreatedInstances);
        Assert.Single(process.Specs);
        Assert.Contains("--installClient", process.Specs[0].Arguments);
    }

    [Fact]
    public async Task Install_Forge_WithoutJava_Fails()
    {
        InMemoryFileSystem fs = new InMemoryFileSystem();
        LoaderInstaller installer = CreateInstaller(BaseHttp(), fs, new StubProcessLauncher());

        Result<LoaderInstallResult> result = await installer.InstallAsync(new LoaderInstallRequest(
            ModLoaderKind.Forge, "1.20.1", "47.2.0", "f", "C:/game"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.Code);
    }

    [Fact]
    public async Task Install_OptiFine_PlacesLibraryAndCreatesInstance()
    {
        InMemoryFileSystem fs = new InMemoryFileSystem();
        const string optifineJar = "https://bmclapi2.bangbang93.com/optifine/1.20.1/HD_U/I6";
        StubHttpTransport http = BaseHttp().OnContent(optifineJar, TestData.MakeBytes(24));
        LoaderInstaller installer = CreateInstaller(http, fs, new StubProcessLauncher());

        Result<LoaderInstallResult> result = await installer.InstallAsync(new LoaderInstallRequest(
            ModLoaderKind.OptiFine, "1.20.1", "1.20.1_HD_U_I6", "1.20.1-OptiFine_I6", "C:/game",
            DownloadUri: new Uri(optifineJar)));

        Assert.True(result.IsSuccess);
        Assert.True(fs.FileExists("C:/game/libraries/optifine/OptiFine/1.20.1_HD_U_I6/OptiFine-1.20.1_HD_U_I6.jar"));
        Assert.True(fs.FileExists("C:/game/versions/1.20.1-OptiFine_I6/1.20.1-OptiFine_I6.json"));
    }

    [Fact]
    public async Task Install_ForgeInstallerFails_ReturnsFailure()
    {
        InMemoryFileSystem fs = new InMemoryFileSystem();
        const string installerJar = "https://maven.minecraftforge.net/net/minecraftforge/forge/1.20.1-47.2.0/forge-1.20.1-47.2.0-installer.jar";
        StubHttpTransport http = BaseHttp().OnContent(installerJar, TestData.MakeBytes(32));
        StubProcessLauncher process = new StubProcessLauncher()
            .On("java", _ => Result<ProcessResult>.Success(new ProcessResult(1, string.Empty, "boom")));
        LoaderInstaller installer = CreateInstaller(http, fs, process);

        Result<LoaderInstallResult> result = await installer.InstallAsync(new LoaderInstallRequest(
            ModLoaderKind.Forge, "1.20.1", "47.2.0", "f", "C:/game", "java"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.IoError, result.Code);
    }
}