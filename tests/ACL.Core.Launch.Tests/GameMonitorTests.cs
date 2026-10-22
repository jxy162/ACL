using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Launch;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Launch.Tests;

public class GameMonitorTests
{
    private static readonly ProcessSpec Spec = new("java.exe", ["-cp", "x", "Main"]);

    [Fact]
    public async Task RunAsync_NormalExit_ReportsExited()
    {
        GameMonitor monitor = new(new StubGameProcessLauncher().OnStart(_ => Result<IGameProcess>.Success(new StubGameProcess(0, "bye"))));

        Result<GameExitInfo> result = await monitor.RunAsync(Spec);

        Assert.True(result.IsSuccess);
        Assert.Equal(GameState.Exited, result.Value!.State);
        Assert.Equal(GameExitReason.NormalExit, result.Value!.Reason);
        Assert.Equal(GameState.Exited, monitor.State);
    }

    [Fact]
    public async Task RunAsync_AccessViolation_ReportsGraphicsDriverError()
    {
        GameMonitor monitor = new(new StubGameProcessLauncher().OnStart(_ => Result<IGameProcess>.Success(new StubGameProcess(-1073741819))));

        Result<GameExitInfo> result = await monitor.RunAsync(Spec);

        Assert.True(result.IsSuccess);
        Assert.Equal(GameState.Crashed, result.Value!.State);
        Assert.Equal(GameExitReason.GraphicsDriverError, result.Value!.Reason);
    }

    [Fact]
    public async Task RunAsync_OutOfMemoryInLog_ReportsMemoryError()
    {
        StubGameProcessLauncher launcher = new StubGameProcessLauncher()
            .OnStart(_ => Result<IGameProcess>.Success(new StubGameProcess(1, "java.lang.OutOfMemoryError: Java heap space")));
        GameMonitor monitor = new(launcher);

        Result<GameExitInfo> result = await monitor.RunAsync(Spec);

        Assert.True(result.IsSuccess);
        Assert.Equal(GameExitReason.MemoryError, result.Value!.Reason);
        Assert.Contains("OutOfMemoryError", result.Value!.LogTail!);
    }

    [Fact]
    public async Task RunAsync_StartFailure_ReturnsFailure()
    {
        GameMonitor monitor = new(new StubGameProcessLauncher().OnFailure());

        Result<GameExitInfo> result = await monitor.RunAsync(Spec);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ProcessStartFailed, result.Code);
        Assert.Equal(GameState.Crashed, monitor.State);
    }

    [Fact]
    public async Task RunAsync_RaisesStateChangedAndProgress()
    {
        GameMonitor monitor = new(new StubGameProcessLauncher().OnStart(_ => Result<IGameProcess>.Success(new StubGameProcess(0))));
        List<GameState> states = [];
        monitor.StateChanged += (_, s) => states.Add(s);
        RecordingSink sink = new();

        await monitor.RunAsync(Spec, sink);

        Assert.Equal([GameState.PreLaunch, GameState.Running, GameState.Exited], states);
        Assert.NotEmpty(sink.Reports);
    }

    [Fact]
    public async Task RunAsync_Cancellation_KillsProcess()
    {
        StubGameProcess process = new(0, delay: TimeSpan.FromSeconds(30));
        GameMonitor monitor = new(new StubGameProcessLauncher().OnStart(_ => Result<IGameProcess>.Success(process)));
        using CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => monitor.RunAsync(Spec, null, cts.Token));
        Assert.True(process.Killed);
    }

    private sealed class RecordingSink : IProgressSink
    {
        public List<ProgressReport> Reports { get; } = [];

        public void Report(ProgressReport report) => Reports.Add(report);
    }
}