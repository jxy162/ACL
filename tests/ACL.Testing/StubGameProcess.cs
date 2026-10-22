using System.Text;
using ACL.Core.Abstractions;

namespace ACL.Testing;

/// <summary>可编程的长时进程启动器桩。</summary>
public sealed class StubGameProcessLauncher : IGameProcessLauncher
{
    private Func<ProcessSpec, Result<IGameProcess>>? _handler;

    public List<ProcessSpec> Specs { get; } = [];

    public StubGameProcessLauncher OnStart(Func<ProcessSpec, Result<IGameProcess>> handler)
    {
        _handler = handler;
        return this;
    }

    public StubGameProcessLauncher OnFailure(ErrorCode code = ErrorCode.ProcessStartFailed)
    {
        _handler = _ => Result<IGameProcess>.Failure(code, "stub start failure");
        return this;
    }

    public Result<IGameProcess> Start(ProcessSpec spec)
    {
        Specs.Add(spec);
        return _handler is null
            ? Result<IGameProcess>.Failure(ErrorCode.ProcessStartFailed, "无处理器")
            : _handler(spec);
    }
}

/// <summary>长时进程桩：可设定退出码 / 输出 / 退出延迟。</summary>
public sealed class StubGameProcess : IGameProcess
{
    private readonly TimeSpan? _delay;

    public StubGameProcess(int exitCode, string output = "", string error = "", TimeSpan? delay = null)
    {
        ExitCode = exitCode;
        _delay = delay;
        StandardOutput = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(output)));
        StandardError = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(error)));
    }

    public int Id => 4242;

    public bool HasExited { get; private set; }

    public int ExitCode { get; }

    public bool Killed { get; private set; }

    public StreamReader StandardOutput { get; }

    public StreamReader StandardError { get; }

    public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        if (_delay is { } delay && delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        HasExited = true;
    }

    public void Kill() => Killed = true;

    public void Dispose()
    {
        StandardOutput.Dispose();
        StandardError.Dispose();
    }
}