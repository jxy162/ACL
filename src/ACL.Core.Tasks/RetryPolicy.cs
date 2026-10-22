using ACL.Core.Abstractions;

namespace ACL.Core.Tasks;

/// <summary>阶段重试策略：次数、初始退避与退避倍数。</summary>
public sealed class RetryPolicy
{
    private readonly Func<ErrorCode, bool>? _shouldRetry;

    /// <summary>不重试（仅执行一次）。</summary>
    public static RetryPolicy None { get; } = new(1, TimeSpan.Zero, 1.0);

    /// <summary>默认策略：最多 3 次、初始 400ms、2 倍退避。</summary>
    public static RetryPolicy Default { get; } = new(3, TimeSpan.FromMilliseconds(400), 2.0);

    /// <summary>构造重试策略。</summary>
    /// <param name="maxAttempts">最大尝试次数（含首次），至少为 1。</param>
    /// <param name="initialDelay">首次退避时长。</param>
    /// <param name="backoffFactor">退避倍数（&gt;0）。</param>
    /// <param name="shouldRetry">错误码谓词；为 <see langword="null"/> 表示全部可重试。</param>
    public RetryPolicy(
        int maxAttempts,
        TimeSpan initialDelay,
        double backoffFactor = 2.0,
        Func<ErrorCode, bool>? shouldRetry = null)
    {
        MaxAttempts = Math.Max(1, maxAttempts);
        InitialDelay = initialDelay < TimeSpan.Zero ? TimeSpan.Zero : initialDelay;
        BackoffFactor = backoffFactor <= 0 ? 1.0 : backoffFactor;
        _shouldRetry = shouldRetry;
    }

    /// <summary>最大尝试次数（含首次）。</summary>
    public int MaxAttempts { get; }

    /// <summary>首次退避时长。</summary>
    public TimeSpan InitialDelay { get; }

    /// <summary>退避倍数。</summary>
    public double BackoffFactor { get; }

    /// <summary>给定错误码是否可重试。</summary>
    public bool ShouldRetry(ErrorCode code) => _shouldRetry?.Invoke(code) ?? true;

    /// <summary>第 <paramref name="failedAttempt"/> 次失败（从 1 开始）后的退避时长。</summary>
    public TimeSpan DelayAfter(int failedAttempt)
    {
        int exponent = Math.Max(0, failedAttempt - 1);
        double ms = InitialDelay.TotalMilliseconds * Math.Pow(BackoffFactor, exponent);
        return TimeSpan.FromMilliseconds(ms);
    }
}