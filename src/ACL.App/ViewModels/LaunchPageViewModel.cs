using System.Collections.ObjectModel;
using ACL.App.Services;
using ACL.Application;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Java;
using ACL.Core.Launch;
using ACL.Core.Version;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>启动页。</summary>
public partial class LaunchPageViewModel : PageViewModelBase
{
    private readonly AppServices _services;
    private readonly AppState _state;

    /// <summary>构造启动页。</summary>
    public LaunchPageViewModel(AppServices services, AppState state)
        : base("启动", "选择版本与账号，生成启动计划并启动游戏。")
    {
        _services = services;
        _state = state;
    }

    /// <summary>状态文本。</summary>
    [ObservableProperty]
    private string _status = "就绪";

    /// <summary>进度（0-1）。</summary>
    [ObservableProperty]
    private double _progress;

    /// <summary>计划摘要。</summary>
    [ObservableProperty]
    private string _planSummary = "尚未生成计划。";

    /// <summary>共享状态（账号 / 版本 / 设置）。</summary>
    public AppState State => _state;

    /// <summary>Hero 中的大号版本标题。</summary>

    public string VersionTitle => string.IsNullOrEmpty(_state.SelectedVersionId) ? "未选择版本" : _state.SelectedVersionId!;

    /// <summary>版本摘要。</summary>

    public string VersionSummary => string.IsNullOrEmpty(_state.SelectedVersionId)
        ? "版本：未选择"
        : $"版本：{_state.SelectedVersionId}";

    /// <summary>账号摘要。</summary>
    public string AccountSummary => $"账号：{_state.PlayerName}（离线）";

    /// <summary>Java 摘要。</summary>
    public string JavaSummary => string.IsNullOrEmpty(_state.SelectedJavaHome)
        ? "尚未探测（到“设置”页探测）"
        : _state.SelectedJavaHome!;

    /// <summary>游戏目录摘要。</summary>
    public string GameDirectorySummary => _services.GameDirectory;

    /// <summary>内存摘要。</summary>
    public string MemorySummary => $"{(int)_state.MinMemoryMb} - {(int)_state.MaxMemoryMb} MB";

    /// <summary>日志。</summary>

    public ObservableCollection<string> Log { get; } = [];

    /// <summary>生成启动计划（dry-run）。</summary>
    [RelayCommand]
    private async Task PlanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Status = "正在读取版本清单…";
        Log.Clear();
        try
        {
            string versionId = string.IsNullOrWhiteSpace(_state.SelectedVersionId)
                ? (await GetDefaultVersionAsync().ConfigureAwait(true) ?? string.Empty)
                : _state.SelectedVersionId!;

            if (string.IsNullOrEmpty(versionId))
            {
                Status = "未找到可用版本，请先在“下载”页刷新。";
                return;
            }

            Result<VersionJson> version = await LoadVersionAsync(versionId).ConfigureAwait(true);
            if (!version.IsSuccess)
            {
                Status = $"读取版本失败：{version.Code}";
                return;
            }

            InstallTargets targets = InstallTargets.ForGameDir(_services.GameDirectory);
            InstallPlan plan = new VersionPlanner().Plan(version.Value!, targets, CurrentOs());
            long total = plan.Downloads.Sum(item => item.Size ?? 0);

            _state.SelectedVersionId = versionId;
            PlanSummary = $"版本 {versionId}：需补齐 {plan.Downloads.Count} 项，约 {total / 1024 / 1024} MB";
            Status = "计划已生成（尚未下载文件）。";
            Log.Add($"[plan] {PlanSummary}");
            foreach (string warning in plan.Warnings)
            {
                Log.Add($"[warn] {warning}");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>安装并启动（会下载缺失文件）。</summary>
    [RelayCommand]
    private async Task InstallAndLaunchAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Status = "准备中…";
        Log.Add($"[1/4] 读取版本 JSON：{_state.SelectedVersionId ?? "未选择"}");
        try
        {
            string versionId = _state.SelectedVersionId ?? string.Empty;
            if (string.IsNullOrEmpty(versionId))
            {
                Status = "请先生成计划以确定版本。";
                return;
            }

            Result<VersionJson> version = await LoadVersionAsync(versionId).ConfigureAwait(true);
            if (!version.IsSuccess)
            {
                Status = $"读取版本失败：{version.Code}";
                return;
            }

            Log.Add("[2/4] 选择 Java 运行时…");
            JavaRuntime? java = await ResolveJavaAsync(version.Value!).ConfigureAwait(true);
            if (java is null)
            {
                Status = "未找到合适的 Java（需要 64 位且版本满足要求）。";
                return;
            }

            AuthSession session = OfflineIdentity.CreateSession(_state.PlayerName);
            InstallTargets targets = InstallTargets.ForGameDir(_services.GameDirectory);
            LaunchSettings settings = new()
            {
                GameDirectory = _services.GameDirectory,
                AssetsRoot = targets.AssetsDir,
                AssetIndexId = version.Value!.AssetIndex?.Id ?? "legacy",
                NativesDirectory = Path.Combine(targets.VersionsDir, versionId, "natives"),
                LibrariesDirectory = targets.LibrariesDir,
                MinMemoryMb = (int)_state.MinMemoryMb,
                MaxMemoryMb = (int)_state.MaxMemoryMb,
            };

            LaunchRequest request = new(
                versionId,
                version.Value!,
                targets,
                java,
                session,
                settings,
                CurrentOs());

            LaunchOrchestrator orchestrator = new(
                _services.Download,
                _services.Natives,
                _services.LaunchPlanner,
                _services.Monitor,
                _services.FileSystem);

            Log.Add("[3/4] 补齐文件 → 解压 natives → 启动进程…");
            string lastStage = string.Empty;
            UiProgressSink sink = new(report =>
            {
                Progress = report.Fraction;
                Status = report.Stage;
                if (!string.Equals(report.Stage, lastStage, StringComparison.Ordinal))
                {
                    lastStage = report.Stage;
                    if (Log.Count < 200)
                    {
                        Log.Add($"[进度] {report.Stage} {report.Fraction:P0}");
                    }
                }
            });

            Result<GameExitInfo> result = await orchestrator
                .RunAsync(request, null, sink)
                .ConfigureAwait(true);

            if (result.IsSuccess)
            {
                GameExitInfo exit = result.Value!;
                _state.AddPlayMinutes(versionId, exit.Duration.TotalMinutes);
                _state.SetLastExit(exit);
                PersistLog(exit.LogTail);
                Status = $"游戏已退出：{exit.State}（{exit.Reason}），本次 {exit.Duration.TotalMinutes:F1} 分钟";
            }
            else
            {
                Status = $"启动失败：{result.Code}";
            }

            Log.Add($"[4/4] {Status}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PersistLog(string? logTail)
    {
        if (string.IsNullOrEmpty(logTail))
        {
            return;
        }

        try
        {
            _services.FileSystem.CreateDirectory(_services.LogDirectory);
            string path = Path.Combine(_services.LogDirectory, "last-launch.log");
            using Stream stream = _services.FileSystem.OpenWrite(path, append: false);
            using StreamWriter writer = new(stream, new System.Text.UTF8Encoding(false));
            writer.Write(logTail);
            _state.LastLogPath = path;
        }
        catch (IOException)
        {
            // 日志落盘失败不影响启动流程。
        }
    }

    private async Task<string?> GetDefaultVersionAsync()
    {
        Result<VersionManifest> manifest = await _services.Versions.GetManifestAsync().ConfigureAwait(true);
        return manifest.IsSuccess ? manifest.Value!.Latest.Release : null;
    }

    private Task<Result<VersionJson>> LoadVersionAsync(string versionId)
        => VersionResolver.ResolveAsync(versionId, (id, _) => LoadRawAsync(id), CancellationToken.None);

    private async Task<Result<VersionJson>> LoadRawAsync(string versionId)
    {
        Result<VersionManifest> manifest = await _services.Versions.GetManifestAsync().ConfigureAwait(true);
        if (!manifest.IsSuccess)
        {
            return Result<VersionJson>.Failure(manifest.Code, manifest.Detail);
        }

        VersionEntry? entry = manifest.Value!.Versions
            .FirstOrDefault(v => string.Equals(v.Id, versionId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return Result<VersionJson>.Failure(ErrorCode.NotFound, versionId);
        }

        MetadataFetcher fetcher = new(_services.Http);
        Result<string> body = await fetcher.FetchStringAsync([entry.Url]).ConfigureAwait(true);
        return body.IsSuccess
            ? VersionJsonParser.Parse(body.Value!)
            : Result<VersionJson>.Failure(body.Code, body.Detail);
    }

    private async Task<JavaRuntime?> ResolveJavaAsync(VersionJson version)
    {
        IReadOnlyList<JavaRuntime> runtimes = await _services.JavaLocator.LocateAsync().ConfigureAwait(true);
        int major = version.JavaVersion?.MajorVersion ?? 8;
        JavaRequirement requirement = new(new JavaVersionRange(new System.Version(major, 0, 0)));
        return new JavaSelector().Select(runtimes, requirement);
    }

    private static OsSpec CurrentOs()
    {
        if (OperatingSystem.IsWindows())
        {
            return OsSpec.WindowsX64;
        }

        return OperatingSystem.IsMacOS() ? OsSpec.MacOsX64 : OsSpec.LinuxX64;
    }
}