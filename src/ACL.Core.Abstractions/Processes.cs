namespace ACL.Core.Abstractions;

/// <summary>进程启动规格。</summary>
/// <param name="FileName">可执行文件路径。</param>
/// <param name="Arguments">参数列表。</param>
/// <param name="WorkingDirectory">工作目录（可空）。</param>
/// <param name="Environment">附加环境变量（可空）。</param>
public sealed record ProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null);

/// <summary>进程执行结果。</summary>
/// <param name="ExitCode">退出码（超时为 -1）。</param>
/// <param name="StandardOutput">标准输出。</param>
/// <param name="StandardError">标准错误。</param>
/// <param name="TimedOut">是否超时被终止。</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut = false)
{
    /// <summary>标准输出与错误的合并文本（便于解析 <c>java -version</c> 这类写入 stderr 的输出）。</summary>
    public string CombinedOutput => StandardError.Length == 0
        ? StandardOutput
        : StandardOutput.Length == 0 ? StandardError : StandardOutput + "\n" + StandardError;
}

/// <summary>进程启动器抽象（短任务：运行并等待结束）。</summary>
public interface IProcessLauncher
{
    /// <summary>运行进程并等待结束（捕获输出）。</summary>
    Task<Result<ProcessResult>> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default);
}

/// <summary>一个已启动的长时进程句柄。</summary>
public interface IGameProcess : IDisposable
{
    /// <summary>进程 ID。</summary>
    int Id { get; }

    /// <summary>进程是否已退出。</summary>
    bool HasExited { get; }

    /// <summary>退出码（未退出时无意义）。</summary>
    int ExitCode { get; }

    /// <summary>标准输出读取器。</summary>
    StreamReader StandardOutput { get; }

    /// <summary>标准错误读取器。</summary>
    StreamReader StandardError { get; }

    /// <summary>等待退出。</summary>
    Task WaitForExitAsync(CancellationToken cancellationToken = default);

    /// <summary>强制终止。</summary>
    void Kill();
}

/// <summary>长时进程启动器（启动游戏并持续读取输出）。</summary>
public interface IGameProcessLauncher
{
    /// <summary>启动进程。</summary>
    Result<IGameProcess> Start(ProcessSpec spec);
}