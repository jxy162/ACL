using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Download;

/// <summary>下载结果状态。</summary>
public enum DownloadStatus
{
    /// <summary>已下载（或续传完成）。</summary>
    Downloaded,

    /// <summary>已存在且校验通过，跳过。</summary>
    Skipped,

    /// <summary>失败。</summary>
    Failed,
}

/// <summary>单个下载项的结果。</summary>
/// <param name="TargetPath">目标路径。</param>
/// <param name="Status">状态。</param>
/// <param name="Code">错误码（成功为 <see cref="ErrorCode.None"/>）。</param>
/// <param name="Bytes">最终落盘的字节数。</param>
/// <param name="Detail">诊断细节。</param>
public sealed record DownloadOutcome(
    string TargetPath,
    DownloadStatus Status,
    ErrorCode Code,
    long Bytes,
    string? Detail = null);

/// <summary>文件校验器。</summary>
public interface IFileChecker
{
    /// <summary>判断文件是否匹配给定哈希。</summary>
    Task<bool> MatchesAsync(string path, HashSpec spec, CancellationToken cancellationToken = default);
}

/// <summary>下载请求拦截器：重写 URL / 请求头（如注入 CurseForge <c>x-api-key</c>、替换镜像）。</summary>
public interface IDownloadInterceptor
{
    /// <summary>重写请求。</summary>
    HttpRequest Intercept(HttpRequest request);
}

/// <summary>下载服务。</summary>
public interface IDownloadService
{
    /// <summary>下载单个文件：校验跳过 → 多源有序回退 → 断点续传 → 校验 → 落盘。</summary>
    Task<Result<DownloadOutcome>> DownloadAsync(
        DownloadItem item,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>批量下载（限并发）。返回每个项的明细；存在失败时 <c>Code</c> 非 <see cref="ErrorCode.None"/>，<c>Value</c> 仍含明细。</summary>
    /// <param name="items">下载项列表。</param>
    /// <param name="maxConcurrency">并发上限；<c>0</c> 表示使用节流配置的默认值。</param>
    /// <param name="progress">进度接收端（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<IReadOnlyList<DownloadOutcome>>> DownloadManyAsync(
        IReadOnlyList<DownloadItem> items,
        int maxConcurrency = 4,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default);
}