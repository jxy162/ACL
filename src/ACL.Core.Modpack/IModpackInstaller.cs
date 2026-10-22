using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Download;

namespace ACL.Core.Modpack;

/// <summary>整合包安装结果。</summary>
/// <param name="OverridesCopied">复制的覆盖文件数。</param>
/// <param name="Downloaded">已下载文件的明细。</param>
/// <param name="Unresolved">仍需外部解析的文件。</param>
public sealed record ModpackInstallResult(
    int OverridesCopied,
    IReadOnlyList<DownloadOutcome> Downloaded,
    IReadOnlyList<ModpackFile> Unresolved);

/// <summary>整合包安装：识别格式 → 复制覆盖文件 → 下载可解析文件。</summary>
public interface IModpackInstaller
{
    /// <summary>识别并解析整合包（只读，不落盘）。</summary>
    Result<ModpackManifest> Parse(string archivePath);

    /// <summary>安装整合包到实例目录。</summary>
    Task<Result<ModpackInstallResult>> InstallAsync(
        ModpackManifest manifest,
        string archivePath,
        string instanceDirectory,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default);
}