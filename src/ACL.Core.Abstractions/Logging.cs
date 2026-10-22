namespace ACL.Core.Abstractions;

/// <summary>结构化日志级别。</summary>
public enum LogLevel
{
    /// <summary>最详细。</summary>
    Trace = 0,

    /// <summary>调试。</summary>
    Debug,

    /// <summary>常规信息。</summary>
    Information,

    /// <summary>警告。</summary>
    Warning,

    /// <summary>错误。</summary>
    Error,

    /// <summary>致命。</summary>
    Critical,
}

/// <summary>
/// 日志抽象。实现需线程安全，并对用户名、令牌、绝对路径等敏感信息脱敏。
/// </summary>
public interface IAppLogger
{
    /// <summary>指定级别是否启用。</summary>
    bool IsEnabled(LogLevel level);

    /// <summary>记录一条日志。</summary>
    void Log(LogLevel level, string message, Exception? exception = null);
}

/// <summary><see cref="IAppLogger"/> 的便捷扩展。</summary>
public static class AppLoggerExtensions
{
    /// <summary>记录 Trace 日志。</summary>
    public static void Trace(this IAppLogger logger, string message) => Log(logger, LogLevel.Trace, message);

    /// <summary>记录 Debug 日志。</summary>
    public static void Debug(this IAppLogger logger, string message) => Log(logger, LogLevel.Debug, message);

    /// <summary>记录 Information 日志。</summary>
    public static void Information(this IAppLogger logger, string message) => Log(logger, LogLevel.Information, message);

    /// <summary>记录 Warning 日志。</summary>
    public static void Warning(this IAppLogger logger, string message, Exception? exception = null)
        => Log(logger, LogLevel.Warning, message, exception);

    /// <summary>记录 Error 日志。</summary>
    public static void Error(this IAppLogger logger, string message, Exception? exception = null)
        => Log(logger, LogLevel.Error, message, exception);

    private static void Log(this IAppLogger logger, LogLevel level, string message, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(level, message, exception);
    }
}