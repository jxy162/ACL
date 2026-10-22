using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Download;

/// <summary>
/// <see cref="IDownloadService"/> 的默认实现。
/// 源策略：按 <see cref="DownloadItem.Primary"/> 与 <see cref="DownloadItem.Mirrors"/> 的**有序**候选逐个尝试，
/// 失败（传输错误 / 非 2xx / 哈希不符）自动切源；命中已有文件且校验通过则跳过；
/// 支持 <c>.part</c> 断点续传，服务端不支持 Range 时回退整文件重下。
/// </summary>
public sealed class DownloadService : IDownloadService
{
    private const int BufferSize = 81920;

    private readonly IHttpTransport _http;
    private readonly IFileSystem _fileSystem;
    private readonly IFileChecker _checker;
    private readonly IAppLogger _logger;
    private readonly IReadOnlyList<IDownloadInterceptor> _interceptors;
    private readonly DownloadThrottle _throttle;
    private readonly RateLimiter _limiter;

    /// <summary>构造下载服务。</summary>
    /// <param name="http">HTTP 传输。</param>
    /// <param name="fileSystem">文件系统。</param>
    /// <param name="checker">文件校验器；为 <see langword="null"/> 时使用默认实现。</param>
    /// <param name="logger">日志；为 <see langword="null"/> 时丢弃。</param>
    /// <param name="interceptors">请求拦截器链。</param>
    /// <param name="throttle">节流配置（全局限速 / 并发上限）；为 <see langword="null"/> 时不限速。</param>
    public DownloadService(
        IHttpTransport http,
        IFileSystem fileSystem,
        IFileChecker? checker = null,
        IAppLogger? logger = null,
        IEnumerable<IDownloadInterceptor>? interceptors = null,
        DownloadThrottle? throttle = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _checker = checker ?? new FileChecker(_fileSystem);
        _logger = logger ?? NullLogger.Instance;
        _interceptors = interceptors is null ? [] : [.. interceptors];
        _throttle = throttle ?? DownloadThrottle.Unlimited;
        _limiter = new RateLimiter(_throttle.BytesPerSecond);
    }

    /// <inheritdoc />
    public async Task<Result<DownloadOutcome>> DownloadAsync(
        DownloadItem item,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        string target = item.TargetPath;
        if (item.Hash is not null
            && _fileSystem.FileExists(target)
            && await _checker.MatchesAsync(target, item.Hash, cancellationToken).ConfigureAwait(false))
        {
            return Result<DownloadOutcome>.Success(new DownloadOutcome(
                target, DownloadStatus.Skipped, ErrorCode.None, _fileSystem.GetFileLength(target)));
        }

        string? directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }

        string partPath = target + ".part";
        IReadOnlyList<Uri> candidates = [item.Primary, .. item.Mirrors];
        string stage = Path.GetFileName(target);

        ErrorCode lastCode = ErrorCode.NotFound;
        string? lastDetail = null;

        foreach (Uri source in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            long existing = _fileSystem.FileExists(partPath) ? _fileSystem.GetFileLength(partPath) : 0;

            Result<HttpResponse> sent = await SendAsync(source, existing > 0 ? existing : null, cancellationToken).ConfigureAwait(false);
            if (!sent.IsSuccess)
            {
                lastCode = sent.Code;
                lastDetail = sent.Detail;
                continue;
            }

            using HttpResponse response = sent.Value!;
            if (!response.IsSuccess || response.Content is null)
            {
                lastCode = response.StatusCode == 404 ? ErrorCode.NotFound : ErrorCode.Network;
                lastDetail = $"HTTP {response.StatusCode}";
                continue;
            }

            bool append = response.IsPartialContent && existing > 0;
            long startOffset = append ? existing : 0;
            long? total = response.ContentLength is long length
                ? (append ? startOffset + length : length)
                : item.Size;

            long written = startOffset;
            byte[] buffer = new byte[BufferSize];
            using (Stream sourceStream = response.Content)
            using (Stream destination = _fileSystem.OpenWrite(partPath, append))
            {
                int read;
                while ((read = await sourceStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    written += read;
                    ReportProgress(progress, stage, written, total);
                    await _limiter.ThrottleAsync(read, cancellationToken).ConfigureAwait(false);
                }

                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (item.Hash is not null
                && !await _checker.MatchesAsync(partPath, item.Hash, cancellationToken).ConfigureAwait(false))
            {
                _logger.Warning($"哈希校验失败，切换源：{target}");
                _fileSystem.Delete(partPath);
                lastCode = ErrorCode.HashMismatch;
                lastDetail = "哈希校验失败。";
                continue;
            }

            _fileSystem.Move(partPath, target, overwrite: true);
            return Result<DownloadOutcome>.Success(new DownloadOutcome(target, DownloadStatus.Downloaded, ErrorCode.None, written));
        }

        _logger.Error($"下载失败：{target}（{lastCode}）");
        return Result<DownloadOutcome>.Failure(lastCode, lastDetail);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<DownloadOutcome>>> DownloadManyAsync(
        IReadOnlyList<DownloadItem> items,
        int maxConcurrency = 0,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        int concurrency = maxConcurrency > 0 ? maxConcurrency : _throttle.MaxConcurrency;
        using SemaphoreSlim gate = new(concurrency);
        Task<Result<DownloadOutcome>>[] tasks = [.. items.Select(async item =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await DownloadAsync(item, progress, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        })];

        Result<DownloadOutcome>[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
        List<DownloadOutcome> outcomes = [.. results.Where(r => r.IsSuccess).Select(r => r.Value!)];
        ErrorCode firstFailure = results.FirstOrDefault(r => !r.IsSuccess).Code;
        return new Result<IReadOnlyList<DownloadOutcome>>(outcomes, firstFailure);
    }

    private async Task<Result<HttpResponse>> SendAsync(Uri uri, long? rangeFrom, CancellationToken cancellationToken)
    {
        HttpRequest request = new(uri, HttpVerb.Get, null, rangeFrom);
        foreach (IDownloadInterceptor interceptor in _interceptors)
        {
            request = interceptor.Intercept(request);
        }

        return await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static void ReportProgress(IProgressSink? progress, string stage, long written, long? total)
    {
        if (progress is null)
        {
            return;
        }

        double fraction = total is long value && value > 0 ? Math.Clamp((double)written / value, 0d, 1d) : 0d;
        progress.Report(new ProgressReport(stage, fraction));
    }

    private sealed class NullLogger : IAppLogger
    {
        public static NullLogger Instance { get; } = new();

        public bool IsEnabled(LogLevel level) => false;

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            // 有意丢弃。
        }
    }
}