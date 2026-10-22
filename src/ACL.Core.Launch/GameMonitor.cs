using System.Diagnostics;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Launch;

/// <summary>游戏进程状态。</summary>
public enum GameState
{
    /// <summary>已启动进程、尚未确认运行。</summary>
    PreLaunch,

    /// <summary>运行中。</summary>
    Running,

    /// <summary>正常退出。</summary>
    Exited,

    /// <summary>异常退出（崩溃）。</summary>
    Crashed,
}

/// <summary>游戏退出信息。</summary>
/// <param name="State">最终状态。</param>
/// <param name="ExitCode">退出码。</param>
/// <param name="Reason">归因。</param>
/// <param name="Duration">运行时长。</param>
/// <param name="LogTail">日志尾部（用于诊断，不直接展示）。</param>
public sealed record GameExitInfo(
    GameState State,
    int ExitCode,
    GameExitReason Reason,
    TimeSpan Duration,
    string? LogTail = null);

/// <summary>游戏进程监控：启动、读取输出、等待退出并归因。</summary>
public interface IGameMonitor
{
    /// <summary>当前状态。</summary>
    GameState State { get; }

    /// <summary>状态变化事件（可能在任意线程触发）。</summary>
    event EventHandler<GameState>? StateChanged;

    /// <summary>运行并监控游戏进程。</summary>
    Task<Result<GameExitInfo>> RunAsync(
        ProcessSpec spec,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary><see cref="IGameMonitor"/> 的默认实现。</summary>
public sealed class GameMonitor : IGameMonitor
{
    private const int LogTailLength = 4000;

    private readonly IGameProcessLauncher _launcher;
    private readonly IAppLogger _logger;

    /// <summary>构造监控器。</summary>
    public GameMonitor(IGameProcessLauncher launcher, IAppLogger? logger = null)
    {
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public GameState State { get; private set; } = GameState.PreLaunch;

    /// <inheritdoc />
    public event EventHandler<GameState>? StateChanged;

    /// <inheritdoc />
    public async Task<Result<GameExitInfo>> RunAsync(
        ProcessSpec spec,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        SetState(GameState.PreLaunch, progress);
        Result<IGameProcess> started = _launcher.Start(spec);
        if (!started.IsSuccess)
        {
            SetState(GameState.Crashed, progress);
            return Result<GameExitInfo>.Failure(started.Code, started.Detail);
        }

        using IGameProcess process = started.Value!;
        SetState(GameState.Running, progress);

        Stopwatch watch = Stopwatch.StartNew();
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw;
        }

        watch.Stop();
        string combined = await SafeReadAsync(output).ConfigureAwait(false);
        string errText = await SafeReadAsync(error).ConfigureAwait(false);
        if (errText.Length > 0)
        {
            combined = combined.Length == 0 ? errText : combined + "\n" + errText;
        }

        GameExitReason reason = GameExitReasons.FromLogAndExitCode(combined, process.ExitCode);
        GameState state = reason == GameExitReason.NormalExit ? GameState.Exited : GameState.Crashed;
        SetState(state, progress);

        if (state == GameState.Crashed)
        {
            _logger.Warning($"游戏异常退出：退出码 {process.ExitCode}，归因 {reason}。");
        }

        return Result<GameExitInfo>.Success(new GameExitInfo(
            state,
            process.ExitCode,
            reason,
            watch.Elapsed,
            Tail(combined)));
    }

    private void SetState(GameState state, IProgressSink? progress)
    {
        State = state;
        double fraction = state switch
        {
            GameState.PreLaunch => 0d,
            GameState.Running => 0.5d,
            _ => 1d,
        };
        progress?.Report(new ProgressReport("launch", fraction, state.ToString()));
        StateChanged?.Invoke(this, state);
    }

    private static string? Tail(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length <= LogTailLength ? text : text[^LogTailLength..];
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

    private sealed class NullLogger : IAppLogger
    {
        public static NullLogger Instance { get; } = new();

        public bool IsEnabled(LogLevel level) => false;

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            // 有意丢弃。
        }
    }
}