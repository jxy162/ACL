namespace ACL.Core.Domain;

/// <summary>游戏进程退出原因（内核只给出原因枚举，文案由宿主本地化）。</summary>
public enum GameExitReason
{
    /// <summary>正常退出。</summary>
    NormalExit,

    /// <summary>Java 级别错误（如类版本不匹配、启动异常）。</summary>
    JavaError,

    /// <summary>内存不足（OutOfMemoryError）。</summary>
    MemoryError,

    /// <summary>显卡 / 驱动相关（如访问违例 0xC0000005）。</summary>
    GraphicsDriverError,

    /// <summary>原生层崩溃（堆损坏、fail-fast 等）。</summary>
    NativeCrash,

    /// <summary>模组相关错误（一般性退出码 1）。</summary>
    ModError,

    /// <summary>未知错误。</summary>
    UnknownError,
}

/// <summary>退出码归因（纯算法）。</summary>
public static class GameExitReasons
{
    /// <summary>由退出码推断原因。</summary>
    public static GameExitReason FromExitCode(int exitCode) => exitCode switch
    {
        0 => GameExitReason.NormalExit,
        1 => GameExitReason.ModError,
        -1073741819 => GameExitReason.GraphicsDriverError,   // 0xC0000005 访问违例
        -1073740791 => GameExitReason.NativeCrash,           // 0xC0000409 fail-fast
        -1073740940 => GameExitReason.NativeCrash,           // 0xC0000374 堆损坏
        -1073741676 => GameExitReason.NativeCrash,           // 0xC0000094 除零
        _ => GameExitReason.UnknownError,
    };

    /// <summary>结合日志文本进一步判断（内存不足 / Java 错误优先于退出码）。</summary>
    public static GameExitReason FromLogAndExitCode(string? output, int exitCode)
    {
        if (!string.IsNullOrEmpty(output))
        {
            if (output.Contains("java.lang.OutOfMemoryError", StringComparison.Ordinal))
            {
                return GameExitReason.MemoryError;
            }

            if (output.Contains("UnsupportedClassVersionError", StringComparison.Ordinal)
                || output.Contains("Could not create the Java Virtual Machine", StringComparison.Ordinal))
            {
                return GameExitReason.JavaError;
            }
        }

        return FromExitCode(exitCode);
    }
}