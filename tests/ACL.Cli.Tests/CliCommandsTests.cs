using ACL.Cli;
using ACL.Core.Auth;
using ACL.Core.Download;
using ACL.Core.Java;
using ACL.Core.Launch;
using ACL.Core.Modpack;
using ACL.Core.Version;
using ACL.Testing;
using Xunit;

namespace ACL.Cli.Tests;

public class CliCommandsTests
{
    private const string ManifestJson = """
    {
      "latest": { "release": "1.20.1", "snapshot": "26.1-snapshot-1" },
      "versions": [
        { "id": "1.20.1", "type": "release", "url": "https://stub/1.20.1.json", "time": "2023-06-12T00:00:00+00:00", "releaseTime": "2023-06-12T00:00:00+00:00", "sha1": "aa", "complianceLevel": 1 }
      ]
    }
    """;

    private const string VersionJson = """
    {
      "id": "1.20.1",
      "mainClass": "net.minecraft.client.main.Main",
      "assets": "5",
      "assetIndex": { "id": "5", "url": "https://stub/assets/5.json", "sha1": "idx", "size": 100 },
      "downloads": { "client": { "url": "https://stub/client.jar", "sha1": "abc", "size": 1048576 } },
      "libraries": [
        { "name": "com.mojang:logging:1.1.1", "downloads": { "artifact": { "path": "com/mojang/logging/1.1.1/logging-1.1.1.jar", "url": "https://stub/logging.jar", "sha1": "lg", "size": 2048 } } }
      ]
    }
    """;

    [Fact]
    public async Task Versions_PrintsLatest()
    {
        (CliCommands commands, StringWriter output) = Create();

        int code = await commands.RunAsync(["versions", "--limit", "1"]);

        Assert.Equal(0, code);
        string text = output.ToString();
        Assert.Contains("最新正式版：1.20.1", text, StringComparison.Ordinal);
        Assert.Contains("1.20.1", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plan_PrintsDownloadCount()
    {
        (CliCommands commands, StringWriter output) = Create();

        int code = await commands.RunAsync(["plan", "1.20.1"]);

        Assert.Equal(0, code);
        string text = output.ToString();
        Assert.Contains("需下载 3 项", text, StringComparison.Ordinal);   // 客户端 + 索引 + 库
    }

    [Fact]
    public async Task LoginOffline_PrintsDeterministicUuid()
    {
        (CliCommands commands, StringWriter output) = Create();

        int code = await commands.RunAsync(["login-offline", "Steve"]);

        Assert.Equal(0, code);
        Assert.Contains("5627dd98e6be3c21b8a8e92344183641", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Modpack_UnrecognizedFile_ReportsFailure()
    {
        (CliCommands commands, StringWriter output) = Create();

        int code = await commands.RunAsync(["modpack", "D:/missing.mrpack"]);

        Assert.Equal(2, code);
        Assert.Contains("解析整合包失败", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownCommand_PrintsHelpAndFails()
    {
        (CliCommands commands, StringWriter output) = Create();

        int code = await commands.RunAsync(["nope"]);

        Assert.Equal(1, code);
        Assert.Contains("ACL 命令行", output.ToString(), StringComparison.Ordinal);
    }

    private static (CliCommands Commands, StringWriter Output) Create()
    {
        InMemoryFileSystem fs = new();
        StubHttpTransport http = new StubHttpTransport()
            .OnText(VersionCatalog.DefaultManifestUri.ToString(), ManifestJson)
            .OnText("https://stub/1.20.1.json", VersionJson);

        StringWriter output = new();
        CliServices services = new(
            new VersionCatalog(http),
            new JavaLocator(fs, new JavaProbe(new StubProcessLauncher(), fs), []),
            new DownloadService(http, fs),
            http,
            new LaunchPlanner(),
            new NativesExtractor(fs),
            new GameMonitor(new StubGameProcessLauncher()),
            new ModpackInstaller(fs, new DownloadService(http, fs)),
            new AuthService(http),
            "D:/mc");

        return (new CliCommands(services, output), output);
    }
}