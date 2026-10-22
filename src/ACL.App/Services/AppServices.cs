using ACL.Core.Abstractions;
using ACL.Core.Auth;
using ACL.Core.Download;
using ACL.Core.Java;
using ACL.Core.Launch;
using ACL.Core.Modpack;
using ACL.Core.Version;
using ACL.Infrastructure;

namespace ACL.App.Services;

/// <summary>界面侧的组合根：在此装配内核实现（内核不感知容器，见 ADR-0004）。</summary>
public sealed class AppServices
{
    private AppServices(
        IHttpTransport http,
        IFileSystem fileSystem,
        IMirrorPolicy mirrorPolicy,
        IDownloadService download,
        IVersionCatalog versions,
        JavaLocator javaLocator,
        IAuthService auth,
        ILaunchPlanner launchPlanner,
        INativesExtractor natives,
        IGameMonitor monitor,
        IModpackInstaller modpack,
        string dataDirectory,
        string gameDirectory)
    {
        Http = http;
        FileSystem = fileSystem;
        MirrorPolicy = mirrorPolicy;
        Download = download;
        Versions = versions;
        JavaLocator = javaLocator;
        Auth = auth;
        LaunchPlanner = launchPlanner;
        Natives = natives;
        Monitor = monitor;
        Modpack = modpack;
        DataDirectory = dataDirectory;
        GameDirectory = gameDirectory;
    }

    /// <summary>HTTP 传输。</summary>
    public IHttpTransport Http { get; }

    /// <summary>文件系统。</summary>
    public IFileSystem FileSystem { get; }

    /// <summary>镜像策略。</summary>
    public IMirrorPolicy MirrorPolicy { get; }

    /// <summary>下载服务。</summary>
    public IDownloadService Download { get; }

    /// <summary>版本目录。</summary>
    public IVersionCatalog Versions { get; }

    /// <summary>Java 搜索。</summary>
    public JavaLocator JavaLocator { get; }

    /// <summary>认证服务。</summary>
    public IAuthService Auth { get; }

    /// <summary>启动计划器。</summary>
    public ILaunchPlanner LaunchPlanner { get; }

    /// <summary>natives 解压。</summary>
    public INativesExtractor Natives { get; }

    /// <summary>游戏监控。</summary>
    public IGameMonitor Monitor { get; }

    /// <summary>整合包安装。</summary>
    public IModpackInstaller Modpack { get; }

    /// <summary>启动器数据目录。</summary>
    public string DataDirectory { get; }

    /// <summary>游戏根目录（默认与启动器同目录的 <c>.minecraft</c>，便携可拷贝）。</summary>
    public string GameDirectory { get; }

    /// <summary>装配默认实现。</summary>
    public static AppServices Create()
    {
        // 便携式：数据目录与游戏目录都放在启动器所在目录，便于整体拷贝与查找。
        string dataDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string gameDirectory = Environment.GetEnvironmentVariable("ACL_GAME_DIR") is { Length: > 0 } custom
            ? custom
            : Path.Combine(dataDirectory, ".minecraft");

        HttpTransport http = new();
        PhysicalFileSystem fileSystem = new();
        MirrorPolicy mirrorPolicy = new();
        DownloadService download = new(http, fileSystem, null, null, null, DownloadThrottle.Unlimited);
        VersionCatalog versions = new(http, mirrorPolicy);
        ProcessLauncher process = new();
        JavaProbe probe = new(process, fileSystem);
        JavaLocator javaLocator = new(
            fileSystem,
            probe,
            [
                Environment.GetEnvironmentVariable("JAVA_HOME") ?? string.Empty,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium"),
            ]);

        return new AppServices(
            http,
            fileSystem,
            mirrorPolicy,
            download,
            versions,
            javaLocator,
            new AuthService(http, new EnvironmentSecretProvider()),
            new LaunchPlanner(),
            new NativesExtractor(fileSystem),
            new GameMonitor(new GameProcessLauncher()),
            new ModpackInstaller(fileSystem, download),
            dataDirectory,
            gameDirectory);
    }
}