using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Download;

namespace ACL.Core.Version.Loaders;

/// <summary>加载器 / 补丁安装请求。</summary>
/// <param name="Kind">加载器种类。</param>
/// <param name="GameVersion">基础 Minecraft 版本。</param>
/// <param name="LoaderVersion">加载器版本（原版为 <see langword="null"/>）。</param>
/// <param name="InstanceName">目标实例名（写入 <c>versions/&lt;name&gt;</c>）。</param>
/// <param name="GameDir">游戏根目录。</param>
/// <param name="JavaPath">Java 可执行文件路径（需要运行安装器的加载器必填）。</param>
/// <param name="BaseInstanceId">组合安装时的基础实例名（如 OptiFine 叠加在 Forge 实例上）。</param>
/// <param name="DownloadUri">安装包直链（可空，通常由目录提供）。</param>
public sealed record LoaderInstallRequest(
    ModLoaderKind Kind,
    string GameVersion,
    string? LoaderVersion,
    string InstanceName,
    string GameDir,
    string? JavaPath = null,
    string? BaseInstanceId = null,
    Uri? DownloadUri = null);

/// <summary>加载器 / 补丁安装结果。</summary>
/// <param name="InstanceId">已安装的实例名。</param>
/// <param name="VersionJsonPath">实例版本 JSON 路径（官方安装器可能写入多个实例）。</param>
/// <param name="CreatedInstances">本次新增的实例名列表。</param>
/// <param name="Downloads">下载明细。</param>
/// <param name="Notes">过程说明（面向用户）。</param>
public sealed record LoaderInstallResult(
    string InstanceId,
    string VersionJsonPath,
    IReadOnlyList<string> CreatedInstances,
    IReadOnlyList<DownloadOutcome> Downloads,
    IReadOnlyList<string> Notes);

/// <summary>加载器 / 补丁安装器：把加载器前置安装到便携式游戏目录，产出可直接启动的实例。</summary>
public interface ILoaderInstaller
{
    /// <summary>执行安装。</summary>
    Task<Result<LoaderInstallResult>> InstallAsync(
        LoaderInstallRequest request,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default);
}