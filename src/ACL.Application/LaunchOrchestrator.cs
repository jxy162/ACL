using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Download;
using ACL.Core.Launch;
using ACL.Core.Version;

namespace ACL.Application;

/// <summary>启动编排：补齐文件 → 解压 natives → 生成启动计划 → 启动并监控。</summary>
public sealed class LaunchOrchestrator
{
    private readonly IDownloadService _download;
    private readonly INativesExtractor _natives;
    private readonly ILaunchPlanner _planner;
    private readonly IGameMonitor _monitor;
    private readonly IFileSystem _fileSystem;
    private readonly IAppLogger _logger;

    /// <summary>构造编排器。</summary>
    public LaunchOrchestrator(
        IDownloadService download,
        INativesExtractor natives,
        ILaunchPlanner planner,
        IGameMonitor monitor,
        IFileSystem fileSystem,
        IAppLogger? logger = null)
    {
        _download = download ?? throw new ArgumentNullException(nameof(download));
        _natives = natives ?? throw new ArgumentNullException(nameof(natives));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>执行一次启动（dry-run 计划 → 下载补齐 → natives → 启动监控）。</summary>
    /// <param name="request">启动请求。</param>
    /// <param name="assets">可选的资源索引（提供时会一并补齐资源对象）。</param>
    /// <param name="progress">进度接收端。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<Result<GameExitInfo>> RunAsync(
        LaunchRequest request,
        AssetIndex? assets = null,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. 生成补全计划（dry-run）
        InstallPlan plan = new VersionPlanner().Plan(request.Version, request.Targets, request.Os);
        List<DownloadItem> items = [.. plan.Downloads];
        if (assets is not null)
        {
            items.AddRange(new AssetPlanner().Plan(assets, request.Targets));
        }

        // 2. 下载补齐
        Result<IReadOnlyList<DownloadOutcome>> downloaded = await _download
            .DownloadManyAsync(items, 0, progress, cancellationToken)
            .ConfigureAwait(false);
        if (!downloaded.IsSuccess)
        {
            _logger.Error($"启动前补齐失败：{downloaded.Code}");
            return Result<GameExitInfo>.Failure(downloaded.Code, downloaded.Detail);
        }

        // 3. 解压 natives
        List<string> nativesJars = CollectNativesJars(request);
        if (nativesJars.Count > 0)
        {
            Result<int> extracted = _natives.Extract(nativesJars, request.Settings.NativesDirectory);
            if (!extracted.IsSuccess)
            {
                return Result<GameExitInfo>.Failure(extracted.Code, extracted.Detail);
            }
        }

        // 4. 生成启动计划
        Result<LaunchPlan> planned = _planner.Plan(request);
        if (!planned.IsSuccess)
        {
            return Result<GameExitInfo>.Failure(planned.Code, planned.Detail);
        }

        LaunchPlan launchPlan = planned.Value!;
        _fileSystem.CreateDirectory(launchPlan.WorkingDir);

        // 5. 启动并监控
        ProcessSpec spec = new(
            launchPlan.JavaPath,
            [.. launchPlan.JvmArgs, .. launchPlan.GameArgs],
            launchPlan.WorkingDir,
            launchPlan.EnvVars);

        return await _monitor.RunAsync(spec, progress, cancellationToken).ConfigureAwait(false);
    }

    private static List<string> CollectNativesJars(LaunchRequest request)
    {
        List<string> jars = [];
        foreach (LibraryRef library in request.Version.Libraries)
        {
            if (!RuleEvaluator.IsAllowed(library.Rules, request.Os))
            {
                continue;
            }

            string? classifier = LibraryPaths.NativesClassifier(library, request.Os.Name);
            if (classifier is null
                || library.Downloads?.Classifiers is not { } classifiers
                || !classifiers.TryGetValue(classifier, out DownloadArtifact? native)
                || native.Path is null)
            {
                continue;
            }

            jars.Add(Path.Combine(request.Targets.LibrariesDir, native.Path.Replace('/', Path.DirectorySeparatorChar)));
        }

        return jars;
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