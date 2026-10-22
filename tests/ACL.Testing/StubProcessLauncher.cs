using ACL.Core.Abstractions;

namespace ACL.Testing;

/// <summary>可编程的进程启动器桩：按可执行文件路径映射固定输出。</summary>
public sealed class StubProcessLauncher : IProcessLauncher
{
    private readonly Dictionary<string, Func<ProcessSpec, Result<ProcessResult>>> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ProcessSpec> _specs = [];

    public IReadOnlyList<ProcessSpec> Specs
    {
        get
        {
            lock (_specs)
            {
                return _specs.ToArray();
            }
        }
    }

    public StubProcessLauncher On(string fileName, Func<ProcessSpec, Result<ProcessResult>> handler)
    {
        _handlers[fileName] = handler;
        return this;
    }

    /// <summary>让指定可执行文件返回给定输出（退出码 0，输出写入 stderr 以贴近 java -version 行为）。</summary>
    public StubProcessLauncher OnOutput(string fileName, string output)
        => On(fileName, _ => Result<ProcessResult>.Success(new ProcessResult(0, string.Empty, output)));

    public StubProcessLauncher OnMissing(string fileName)
        => On(fileName, _ => Result<ProcessResult>.Failure(ErrorCode.ProcessStartFailed, "not found"));

    public Task<Result<ProcessResult>> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default)
    {
        lock (_specs)
        {
            _specs.Add(spec);
        }

        return Task.FromResult(_handlers.TryGetValue(spec.FileName, out Func<ProcessSpec, Result<ProcessResult>>? handler)
            ? handler(spec)
            : Result<ProcessResult>.Failure(ErrorCode.ProcessStartFailed, "无处理器"));
    }
}