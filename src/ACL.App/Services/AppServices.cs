using ACL.Core.Abstractions;
using ACL.Core.Auth;
using ACL.Core.Download;
using ACL.Core.Java;
using ACL.Core.Launch;
using ACL.Core.Modpack;
using ACL.Core.Resources;
using ACL.Core.Version;
using ACL.Infrastructure;

namespace ACL.App.Services;

/// <summary>界面侧的组合根：在此装配内核实现（内核不感知容器，见 ADR-0004）。</summary>
public sealed class AppServices
{
    private AppServices(
        IHttpTransport http,
        IFileSystem fileSystem,
        ISecretProvider secrets,
        IMirrorPolicy mirrorPolicy,
        IDownloadService download,
        IVersionCatalog versions,
        JavaLocator javaLocator,
        IAuthService auth,
        ILaunchPlanner launchPlanner,
        INativesExtractor natives,
        IGameMonitor monitor,
        IModpackInstaller modpack,
        IResourceIndex modrinth,
        IResourceIndex curseForge,
        IAppSettingsStore settings,
        string dataDirectory,
        string gameDirectory)
    {
        Http = http;
        FileSystem = fileSystem;
        Secrets = secrets;
        MirrorPolicy = mirrorPolicy;
        Download = download;
        Versions = versions;
        JavaLocator = javaLocator;
        Auth = auth;
        LaunchPlanner = launchPlanner;
        Natives = natives;
        Monitor = monitor;
        Modpack = modpack;
        Modrinth = modrinth;
        CurseForge = curseForge;
        Settings = settings;
        DataDirectory = dataDirectory;
        GameDirectory = gameDirectory;
    }

    /// <summary>HTTP 传输。</summary>
    public IHttpTransport Http { get; }

    /// <summary>文件系统。</summary>
    public IFileSystem FileSystem { get; }

    /// <summary>密钥来源（便携文件 + ACL_* + PCL_* 兼容）。</summary>
    public ISecretProvider Secrets { get; }

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

    /// <summary>Modrinth 资源索引。</summary>
    public IResourceIndex Modrinth { get; }

    /// <summary>CurseForge 资源索引。</summary>
    public IResourceIndex CurseForge { get; }

    /// <summary>设置存储（便携 <c>ACL.config.json</c>）。</summary>
    public IAppSettingsStore Settings { get; }

    /// <summary>启动器数据目录（便携：与启动器同目录）。</summary>
    public string DataDirectory { get; }

    /// <summary>日志目录。</summary>
    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>游戏根目录（默认同目录 <c>.minecraft</c>）。</summary>
    public string GameDirectory { get; }

    /// <summary>是否已配置 CurseForge 密钥。</summary>
    public bool HasCurseForgeKey => !string.IsNullOrEmpty(Secrets.GetSecret(CurseForgeClient.ApiKeySecretName));

    /// <summary>是否已配置微软应用 Client ID。</summary>
    public bool HasMicrosoftClientId => !string.IsNullOrEmpty(Secrets.GetSecret(AuthService.MicrosoftClientIdSecretKey));

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

        // 密钥来源（ACL 自有，不依赖第三方命名）：
        //   1) 启动器目录下的便携文件 ACL.secrets.json
        //   2) 环境变量 ACL_MS_CLIENT_ID / ACL_CURSEFORGE_API_KEY
        ISecretProvider secrets = new CompositeSecretProvider(
            new FileSecretProvider(fileSystem, Path.Combine(dataDirectory, "ACL.secrets.json")),
            new EnvironmentSecretProvider());

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

        CurseForgeClient curseForge = new(http, secrets);
        return new AppServices(
            http,
            fileSystem,
            secrets,
            mirrorPolicy,
            download,
            versions,
            javaLocator,
            new AuthService(http, secrets),
            new LaunchPlanner(),
            new NativesExtractor(fileSystem),
            new GameMonitor(new GameProcessLauncher()),
            new ModpackInstaller(fileSystem, download, null, new CurseForgeModpackFileResolver(curseForge)),
            new ModrinthClient(http),
            curseForge,
            new PortableSettingsStore(fileSystem, Path.Combine(dataDirectory, "ACL.config.json")),
            dataDirectory,
            gameDirectory);
    }
}