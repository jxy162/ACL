using ACL.Core.Abstractions;

namespace ACL.Infrastructure;

/// <summary>丢弃所有日志的 <see cref="IAppLogger"/> 实现（测试 / 默认回退用）。</summary>
public sealed class NullAppLogger : IAppLogger
{
    /// <summary>共享实例。</summary>
    public static NullAppLogger Instance { get; } = new();

    /// <inheritdoc />
    public bool IsEnabled(LogLevel level) => false;

    /// <inheritdoc />
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        // 有意丢弃。
    }
}