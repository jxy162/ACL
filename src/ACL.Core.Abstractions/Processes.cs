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

/// <summary>进程启动器抽象。</summary>
public interface IProcessLauncher
{
    /// <summary>运行进程并等待结束（捕获输出）。</summary>
    Task<Result<ProcessResult>> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default);
}