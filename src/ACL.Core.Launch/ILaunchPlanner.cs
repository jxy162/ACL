using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version;

namespace ACL.Core.Launch;

/// <summary>启动设置（显式传参，替代读取全局配置）。</summary>
public sealed record LaunchSettings
{
    /// <summary>游戏根目录（工作目录）。</summary>
    public required string GameDirectory { get; init; }

    /// <summary>资源根目录。</summary>
    public required string AssetsRoot { get; init; }

    /// <summary>资源索引名。</summary>
    public required string AssetIndexId { get; init; }

    /// <summary>natives 目录。</summary>
    public required string NativesDirectory { get; init; }

    /// <summary>最大堆（MB）。</summary>
    public int MaxMemoryMb { get; init; } = 2048;

    /// <summary>最小堆（MB）；&lt;=0 表示不设置。</summary>
    public int MinMemoryMb { get; init; } = 512;

    /// <summary>窗口宽。</summary>
    public int WindowWidth { get; init; } = 854;

    /// <summary>窗口高。</summary>
    public int WindowHeight { get; init; } = 480;

    /// <summary>启动器名（用于 <c>${launcher_name}</c>）。</summary>
    public string LauncherName { get; init; } = "ACL";

    /// <summary>启动器版本（用于 <c>${launcher_version}</c>）。</summary>
    public string LauncherVersion { get; init; } = "0.1.0";

    /// <summary>库目录（用于旧版 <c>${library_directory}</c>）。</summary>
    public string? LibrariesDirectory { get; init; }

    /// <summary>authlib-injector 的 jar 路径（第三方登录）。</summary>
    public string? AuthlibInjectorJar { get; init; }

    /// <summary>第三方（Yggdrasil）API 根地址。</summary>
    public string? ThirdPartyApiRoot { get; init; }

    /// <summary>版本类型覆盖（用于 <c>${version_type}</c>）。</summary>
    public string? VersionType { get; init; }

    /// <summary>追加的 JVM 参数。</summary>
    public IReadOnlyList<string>? ExtraJvmArguments { get; init; }

    /// <summary>追加的游戏参数。</summary>
    public IReadOnlyList<string>? ExtraGameArguments { get; init; }

    /// <summary>追加的环境变量。</summary>
    public IReadOnlyDictionary<string, string>? ExtraEnvironment { get; init; }
}

/// <summary>一次启动计划请求。</summary>
/// <param name="VersionId">版本名。</param>
/// <param name="Version">（已合并的）版本 JSON。</param>
/// <param name="Targets">安装目标目录。</param>
/// <param name="Java">选定的 Java 运行时。</param>
/// <param name="Session">认证会话。</param>
/// <param name="Settings">启动设置。</param>
/// <param name="Os">目标操作系统规格。</param>
public sealed record LaunchRequest(
    string VersionId,
    VersionJson Version,
    InstallTargets Targets,
    JavaRuntime Java,
    AuthSession Session,
    LaunchSettings Settings,
    OsSpec Os);

/// <summary>启动计划生成器（纯计算，可 dry-run）。</summary>
public interface ILaunchPlanner
{
    /// <summary>生成启动计划。</summary>
    Result<LaunchPlan> Plan(LaunchRequest request);
}