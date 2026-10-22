using System.IO.Compression;
using System.Text;
using ACL.Application;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Download;
using ACL.Core.Launch;
using ACL.Core.Version;
using ACL.Testing;
using Xunit;

namespace ACL.Integration.Tests;

/// <summary>端到端（桩服务）：补齐 → 解压 natives → 启动计划 → 启动并监控。</summary>
public class LaunchOrchestratorTests
{
    private const string ClientUrl = "https://stub/client.jar";
    private const string LibUrl = "https://stub/lwjgl.jar";
    private const string NativesUrl = "https://stub/lwjgl-natives.jar";
    private const string IndexUrl = "https://stub/assets/indexes/5.json";

    [Fact]
    public async Task RunAsync_CompletesFullLaunchPipeline()
    {
        byte[] clientBytes = TestData.MakeBytes(100, 1);
        byte[] libBytes = TestData.MakeBytes(80, 2);
        byte[] nativesZip = MakeZip(("lwjgl.dll", "dll"));
        byte[] assetBytes = TestData.MakeBytes(16, 3);
        string assetHash = TestData.Sha1Of(assetBytes).Value;
        byte[] assetIndexBytes = Encoding.UTF8.GetBytes(
            $"{{\"objects\":{{\"a.bin\":{{\"hash\":\"{assetHash}\",\"size\":{assetBytes.Length}}}}}}}");

        InMemoryFileSystem fs = new();
        StubHttpTransport http = new StubHttpTransport()
            .OnContent(ClientUrl, clientBytes)
            .OnContent(LibUrl, libBytes)
            .OnContent(NativesUrl, nativesZip)
            .OnContent(IndexUrl, assetIndexBytes)
            .OnContent($"https://resources.download.minecraft.net/{assetHash[..2]}/{assetHash}", assetBytes);

        InstallTargets targets = InstallTargets.ForGameDir("D:/mc");
        LaunchSettings settings = new()
        {
            GameDirectory = "D:/mc",
            AssetsRoot = targets.AssetsDir,
            AssetIndexId = "5",
            NativesDirectory = "D:/mc/versions/1.20.1/natives",
            LibrariesDirectory = targets.LibrariesDir,
        };

        VersionJson version = BuildVersion(clientBytes, libBytes, nativesZip, assetIndexBytes);
        AssetIndex assets = AssetIndexParser.Parse(Encoding.UTF8.GetString(assetIndexBytes), "5").Value!;
        LaunchRequest request = new(
            "1.20.1",
            version,
            targets,
            new JavaRuntime("C:/Java/jdk-17", new System.Version(17, 0, 9), true),
            new AuthSession(AccountType.Offline, "uuid", "Steve", "0"),
            settings,
            OsSpec.WindowsX64);

        StubGameProcessLauncher processLauncher = new StubGameProcessLauncher()
            .OnStart(_ => Result<IGameProcess>.Success(new StubGameProcess(0, "bye")));

        LaunchOrchestrator orchestrator = new(
            new DownloadService(http, fs),
            new NativesExtractor(fs),
            new LaunchPlanner(),
            new GameMonitor(processLauncher),
            fs);

        Result<GameExitInfo> result = await orchestrator.RunAsync(request, assets);

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        Assert.Equal(GameState.Exited, result.Value!.State);

        // 补齐产物
        Assert.True(fs.FileExists(Path.Combine(targets.VersionsDir, "1.20.1", "1.20.1.jar")));
        Assert.True(fs.FileExists(Path.Combine(targets.LibrariesDir, "org", "lwjgl", "lwjgl", "3.3.1", "lwjgl-3.3.1.jar")));
        Assert.True(fs.FileExists(Path.Combine(targets.AssetsDir, "indexes", "5.json")));
        Assert.True(fs.FileExists(Path.Combine(targets.AssetsDir, "objects", assetHash[..2], assetHash)));

        // natives 已解压
        Assert.True(fs.FileExists(Path.Combine(settings.NativesDirectory, "lwjgl.dll")));

        // 启动参数
        ProcessSpec spec = Assert.Single(processLauncher.Specs);
        Assert.Equal(Path.Combine("C:/Java/jdk-17", "bin", OperatingSystem.IsWindows() ? "java.exe" : "java"), spec.FileName);
        Assert.Contains("-cp", spec.Arguments);
        Assert.Contains("--username", spec.Arguments);
        Assert.Contains("Steve", spec.Arguments);
    }

    private static VersionJson BuildVersion(byte[] client, byte[] library, byte[] natives, byte[] assetIndex)
        => new(
            "1.20.1",
            null,
            "net.minecraft.client.main.Main",
            "5",
            "release",
            new AssetIndexRef("5", new Uri(IndexUrl), TestData.Sha1Of(assetIndex).Value, assetIndex.Length),
            new JavaVersionRef("java-runtime-gamma", 17),
            new VersionDownloads(new DownloadArtifact(new Uri(ClientUrl), TestData.Sha1Of(client).Value, client.Length)),
            [
                new LibraryRef(
                    "org.lwjgl:lwjgl:3.3.1",
                    new LibraryDownloads(
                        new DownloadArtifact(new Uri(LibUrl), TestData.Sha1Of(library).Value, library.Length, "org/lwjgl/lwjgl/3.3.1/lwjgl-3.3.1.jar"),
                        new Dictionary<string, DownloadArtifact>
                        {
                            ["natives-windows"] = new DownloadArtifact(new Uri(NativesUrl), TestData.Sha1Of(natives).Value, natives.Length, "org/lwjgl/lwjgl/3.3.1/lwjgl-3.3.1-natives-windows.jar"),
                        }),
                    null,
                    new Dictionary<string, string> { ["windows"] = "natives-windows" }),
            ],
            [new ArgumentEntry(["--username", "${auth_player_name}"]), new ArgumentEntry(["--version", "${version_name}"])],
            [new ArgumentEntry(["-cp", "${classpath}"])],
            null);

    private static byte[] MakeZip(params (string Name, string Content)[] entries)
    {
        using MemoryStream stream = new();
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name);
                using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }
}