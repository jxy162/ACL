namespace ACL.Core.Download;

/// <summary>下载节流配置：全局限速与默认并发上限。</summary>
public sealed class DownloadThrottle
{
    /// <summary>不限速，默认并发 4。</summary>
    public static DownloadThrottle Unlimited { get; } = new(0, 4);

    /// <summary>构造节流配置。</summary>
    /// <param name="bytesPerSecond">全局每秒字节上限；<c>0</c> 表示不限速。</param>
    /// <param name="maxConcurrency">默认并发上限。</param>
    public DownloadThrottle(long bytesPerSecond, int maxConcurrency = 4)
    {
        BytesPerSecond = bytesPerSecond < 0 ? 0 : bytesPerSecond;
        MaxConcurrency = maxConcurrency <= 0 ? 1 : maxConcurrency;
    }

    /// <summary>每秒字节上限；<c>0</c> 表示不限速。</summary>
    public long BytesPerSecond { get; }

    /// <summary>默认并发上限。</summary>
    public int MaxConcurrency { get; }

    /// <summary>是否启用限速。</summary>
    public bool IsLimited => BytesPerSecond > 0;
}

/// <summary>基于平均速率的全局限速器（多下载共享同一实例）。</summary>
internal sealed class RateLimiter
{
    private readonly long _bytesPerSecond;
    private readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();
    private long _served;

    public RateLimiter(long bytesPerSecond)
    {
        _bytesPerSecond = bytesPerSecond;
    }

    /// <summary>按已传输字节推进限速。</summary>
    public async Task ThrottleAsync(long bytes, CancellationToken cancellationToken)
    {
        if (_bytesPerSecond <= 0 || bytes <= 0)
        {
            return;
        }

        _served += bytes;
        double expectedSeconds = (double)_served / _bytesPerSecond;
        double delaySeconds = expectedSeconds - _watch.Elapsed.TotalSeconds;
        if (delaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
        }
    }
}