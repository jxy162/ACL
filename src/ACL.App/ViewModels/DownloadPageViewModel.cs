using System.Collections.ObjectModel;
using ACL.App.Services;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Download;
using ACL.Core.Version;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>下载页：版本清单与安装。</summary>
public partial class DownloadPageViewModel : PageViewModelBase
{
    private readonly AppServices _services;
    private readonly AppState _state;

    /// <summary>构造下载页。</summary>
    public DownloadPageViewModel(AppServices services, AppState state)
        : base("下载", "从官方 / 镜像获取版本清单，补齐文件到 ACL 数据目录。")
    {
        _services = services;
        _state = state;
    }

    /// <summary>版本列表。</summary>
    public ObservableCollection<VersionRowViewModel> Versions { get; } = [];

    /// <summary>状态。</summary>
    [ObservableProperty]
    private string _status = "点击“刷新”获取版本清单。";

    /// <summary>进度。</summary>
    [ObservableProperty]
    private double _progress;

    /// <summary>安装目标目录提示。</summary>
    public string GameDirectory => _services.GameDirectory;

    /// <summary>刷新版本清单。</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Status = "正在获取版本清单…";
        try
        {
            Result<VersionManifest> manifest = await _services.Versions.GetManifestAsync().ConfigureAwait(true);
            if (!manifest.IsSuccess)
            {
                Status = $"获取失败：{manifest.Code}（{manifest.Detail}）";
                return;
            }

            Versions.Clear();
            foreach (VersionEntry entry in manifest.Value!.Versions.Take(60))
            {
                Versions.Add(new VersionRowViewModel(entry.Id, entry.Type, entry.ReleaseTime) { Install = InstallCommand });
            }

            Status = $"共 {manifest.Value.Versions.Count} 个版本，已显示前 {Versions.Count} 个。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>安装所选版本（补齐客户端 / 库 / 资源索引）。</summary>
    [RelayCommand]
    private async Task InstallAsync(VersionRowViewModel? row)
    {
        if (IsBusy || row is null)
        {
            return;
        }

        IsBusy = true;
        Status = $"准备安装 {row.Id}…";
        Progress = 0;
        try
        {
            MetadataFetcher fetcher = new(_services.Http);
            Result<VersionManifest> manifest = await _services.Versions.GetManifestAsync().ConfigureAwait(true);
            VersionEntry? entry = manifest.IsSuccess
                ? manifest.Value!.Versions.FirstOrDefault(v => v.Id == row.Id)
                : null;
            if (entry is null)
            {
                Status = "未找到版本条目。";
                return;
            }

            Result<string> body = await fetcher.FetchStringAsync([entry.Url]).ConfigureAwait(true);
            if (!body.IsSuccess)
            {
                Status = $"读取版本 JSON 失败：{body.Code}";
                return;
            }

            Result<VersionJson> parsed = VersionJsonParser.Parse(body.Value!);
            if (!parsed.IsSuccess)
            {
                Status = $"解析版本 JSON 失败：{parsed.Code}";
                return;
            }

            InstallTargets targets = InstallTargets.ForGameDir(_services.GameDirectory);
            InstallPlan plan = new VersionPlanner().Plan(parsed.Value!, targets, OsSpecForCurrent());
            UiProgressSink sink = new(report =>
            {
                Progress = report.Fraction;
                Status = $"{row.Id} · {report.Stage}";
            });

            Result<IReadOnlyList<DownloadOutcome>> result = await _services.Download
                .DownloadManyAsync(plan.Downloads, 0, sink)
                .ConfigureAwait(true);

            Status = result.IsSuccess
                ? $"已补齐 {row.Id}（{plan.Downloads.Count} 项）。"
                : $"安装失败：{result.Code}";
            _state.SelectedVersionId = row.Id;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static OsSpec OsSpecForCurrent()
        => OperatingSystem.IsWindows() ? OsSpec.WindowsX64
            : OperatingSystem.IsMacOS() ? OsSpec.MacOsX64
            : OsSpec.LinuxX64;
}