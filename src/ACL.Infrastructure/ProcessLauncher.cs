using System.ComponentModel;
using System.Diagnostics;
using ACL.Core.Abstractions;

namespace ACL.Infrastructure;

/// <summary>基于 <see cref="Process"/> 的 <see cref="IProcessLauncher"/> 实现（捕获输出与超时）。</summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    private readonly TimeSpan _timeout;

    /// <summary>构造启动器。</summary>
    /// <param name="timeout">超时时长；默认 20 秒。</param>
    public ProcessLauncher(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(20);
    }

    /// <inheritdoc />
    public async Task<Result<ProcessResult>> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        ProcessStartInfo startInfo = new()
        {
            FileName = spec.FileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in spec.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrEmpty(spec.WorkingDirectory))
        {
            startInfo.WorkingDirectory = spec.WorkingDirectory;
        }

        if (spec.Environment is not null)
        {
            foreach (KeyValuePair<string, string> pair in spec.Environment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        try
        {
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"无法启动进程：{spec.FileName}");

            Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);

            using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_timeout);

            try
            {
                await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                return Result<ProcessResult>.Success(
                    new ProcessResult(-1, await SafeReadAsync(output), await SafeReadAsync(error), TimedOut: true));
            }

            return Result<ProcessResult>.Success(
                new ProcessResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return Result<ProcessResult>.Failure(ErrorCode.ProcessStartFailed, ex.Message);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            // 忽略终止失败。
        }
    }

    private static async Task<string> SafeReadAsync(Task<string> read)
    {
        try
        {
            return await read.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            return string.Empty;
        }
    }
}