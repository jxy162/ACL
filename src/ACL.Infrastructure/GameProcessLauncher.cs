using System.ComponentModel;
using System.Diagnostics;
using ACL.Core.Abstractions;

namespace ACL.Infrastructure;

/// <summary>基于 <see cref="Process"/> 的 <see cref="IGameProcessLauncher"/> 实现。</summary>
public sealed class GameProcessLauncher : IGameProcessLauncher
{
    /// <inheritdoc />
    public Result<IGameProcess> Start(ProcessSpec spec)
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
            Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"无法启动进程：{spec.FileName}");
            return Result<IGameProcess>.Success(new GameProcess(process));
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return Result<IGameProcess>.Failure(ErrorCode.ProcessStartFailed, ex.Message);
        }
    }

    private sealed class GameProcess : IGameProcess
    {
        private readonly Process _process;

        public GameProcess(Process process) => _process = process;

        public int Id => _process.Id;

        public bool HasExited => _process.HasExited;

        public int ExitCode => _process.ExitCode;

        public StreamReader StandardOutput => _process.StandardOutput;

        public StreamReader StandardError => _process.StandardError;

        public Task WaitForExitAsync(CancellationToken cancellationToken = default)
            => _process.WaitForExitAsync(cancellationToken);

        public void Kill()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or Win32Exception)
            {
                // 忽略终止失败。
            }
        }

        public void Dispose() => _process.Dispose();
    }
}