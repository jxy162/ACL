using System.Collections.ObjectModel;
using System.Windows.Input;
using ACL.App.Services;
using ACL.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>下载页：按"正式版 / 快照版 / 预览版 / 远古版"分区列出全部版本，并可进入安装二级页面。</summary>
public partial class DownloadPageViewModel : PageViewModelBase
{
    private readonly AppServices _services;
    private readonly AppState _state;
    private readonly ILauncherHost _host;

    /// <summary>构造下载页。</summary>
    public DownloadPageViewModel(AppServices services, AppState state, ILauncherHost host)
        : base("下载", "获取全部版本（正式版 / 快照版 / 预览版 / 远古版），选中后进入安装页面配置加载器。")
    {
        _services = services;
        _state = state;
        _host = host;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenInstallCommand = new RelayCommand<VersionRowViewModel>(row =>
        {
            if (row is not null)
            {
                _state.SelectedVersion = row;
                _host.OpenInstallPage(row);
            }
        });
    }

    /// <summary>分类筛选项。</summary>
    public ObservableCollection<string> KindFilters { get; } = ["全部", "正式版", "快照版", "预览版", "远古版"];

    /// <summary>当前分类。</summary>
    [ObservableProperty]
    private string _kindFilter = "正式版";

    /// <summary>关键字。</summary>
    [ObservableProperty]
    private string _filter = string.Empty;

    /// <summary>筛选后的列表。</summary>
    public ObservableCollection<VersionRowViewModel> Filtered { get; } = [];

    /// <summary>状态文本。</summary>
    [ObservableProperty]
    private string _status = "点击“刷新版本清单”获取全部版本。";

    /// <summary>分类计数文本。</summary>
    [ObservableProperty]
    private string _counts = string.Empty;

    /// <summary>安装目标目录提示。</summary>
    public string GameDirectory => _services.GameDirectory;

    /// <summary>刷新命令。</summary>
    public ICommand RefreshCommand { get; }

    /// <summary>进入安装页面命令。</summary>
    public ICommand OpenInstallCommand { get; }

    partial void OnFilterChanged(string value) => ApplyFilter();
    partial void OnKindFilterChanged(string value) => ApplyFilter();

    /// <summary>重建列表。</summary>
    public void ApplyFilter()
    {
        Filtered.Clear();
        string keyword = Filter.Trim();
        foreach (VersionRowViewModel row in _state.Versions)
        {
            if (KindFilter != "全部" && row.CategoryLabel != KindFilter)
            {
                continue;
            }

            if (keyword.Length > 0 && !row.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            row.OpenInstall = OpenInstallCommand;
            Filtered.Add(row);
        }

        Counts = $"共 {_state.Versions.Count} 个版本 · 正式版 {Count("正式版")} / 快照 {Count("快照版")} / 预览 {Count("预览版")} / 远古 {Count("远古版")}";
    }

    private int Count(string category) => _state.Versions.Count(row => row.CategoryLabel == category);

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
            Status = await VersionLoader.LoadIntoAsync(_services, _state).ConfigureAwait(true);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Status = $"刷新失败：{ex.GetType().Name}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>页面激活时调用。</summary>
    public void OnActivated() => ApplyFilter();
}