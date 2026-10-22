using System.Collections.ObjectModel;
using System.Windows.Input;
using ACL.App.Services;
using ACL.Core.Abstractions;
using ACL.Core.Version;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>
/// 启动页：直接按版本列表选择版本，本机已安装即可启动；
/// 未安装则跳转到"安装"二级页面。这里不再有"启动计划"。
/// </summary>
public partial class LaunchPageViewModel : PageViewModelBase
{
    private readonly AppServices _services;
    private readonly AppState _state;
    private readonly ILauncherHost _host;

    /// <summary>构造启动页。</summary>
    public LaunchPageViewModel(AppServices services, AppState state, ILauncherHost host)
        : base("启动", "选择版本 → 启动；未安装的版本会引导到安装页面配置加载器。")
    {
        _services = services;
        _state = state;
        _host = host;

        LaunchSelectedCommand = new AsyncRelayCommand(LaunchSelectedAsync);
        InstallSelectedCommand = new RelayCommand(InstallSelected, () => _state.SelectedVersion is not null);
        LaunchInstanceCommand = new AsyncRelayCommand<string>(id => id is null ? Task.CompletedTask : _host.LaunchInstanceAsync(id));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);

        _state.Instances.CollectionChanged += (_, _) => ApplyFilter();
        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.SelectedVersionId) or nameof(AppState.SelectedVersion))
            {
                OnPropertyChanged(nameof(HeroTitle));
                OnPropertyChanged(nameof(HeroSubtitle));
                OnPropertyChanged(nameof(CanLaunchSelected));
                LaunchSelectedCommand.NotifyCanExecuteChanged();
                InstallSelectedCommand.NotifyCanExecuteChanged();
            }
        };
    }

    /// <summary>共享状态。</summary>
    public AppState State => _state;

    /// <summary>筛选后的版本列表。</summary>
    public ObservableCollection<VersionRowViewModel> Versions { get; } = [];

    /// <summary>本机已安装实例（快捷启动）。</summary>
    public ObservableCollection<InstalledInstanceRow> LocalInstances { get; } = [];

    /// <summary>是否有本机实例。</summary>
    public bool HasLocalInstances => LocalInstances.Count > 0;

    /// <summary>分类筛选。</summary>
    public ObservableCollection<string> Categories { get; } = ["全部", "正式版", "快照版", "预览版", "远古版"];

    /// <summary>当前分类。</summary>
    [ObservableProperty]
    private string _category = "全部";

    /// <summary>关键字。</summary>
    [ObservableProperty]
    private string _search = string.Empty;

    /// <summary>Hero 标题。</summary>
    public string HeroTitle => string.IsNullOrWhiteSpace(_state.SelectedVersionId) ? "未选择版本" : _state.SelectedVersionId!;

    /// <summary>Hero 副标题。</summary>
    public string HeroSubtitle => _state.SelectedVersion is { } row
        ? $"{row.CategoryLabel} · 发布于 {row.Released}" + (row.IsInstalled ? " · 已安装" : " · 尚未安装")
        : "从右侧版本列表中选择一个版本";

    /// <summary>选中版本是否可启动。</summary>
    public bool CanLaunchSelected => _state.SelectedVersion is { IsInstalled: true };

    /// <summary>状态文本。</summary>
    [ObservableProperty]
    private string _status = "正在准备版本列表…";

    /// <summary>启动选中版本命令。</summary>
    public IAsyncRelayCommand LaunchSelectedCommand { get; }

    /// <summary>安装选中版本命令。</summary>
    public IRelayCommand InstallSelectedCommand { get; }

    /// <summary>启动实例命令。</summary>
    public ICommand LaunchInstanceCommand { get; }

    /// <summary>刷新版本清单命令。</summary>
    public ICommand RefreshCommand { get; }

    /// <summary>版本选择命令（列表点击即选中）。</summary>
    [RelayCommand]
    private void Select(VersionRowViewModel? row)
    {
        if (row is not null)
        {
            _state.SelectedVersion = row;
            OnPropertyChanged(nameof(HeroTitle));
            OnPropertyChanged(nameof(HeroSubtitle));
            OnPropertyChanged(nameof(CanLaunchSelected));
            LaunchSelectedCommand.NotifyCanExecuteChanged();
            InstallSelectedCommand.NotifyCanExecuteChanged();

            // 选中即在本机实例中定位。
            if (row.IsInstalled)
            {
                _ = _host;
            }
        }
    }

    partial void OnCategoryChanged(string value) => ApplyFilter();
    partial void OnSearchChanged(string value) => ApplyFilter();

    /// <summary>重建列表。</summary>
    public void ApplyFilter()
    {
        Versions.Clear();
        string keyword = Search.Trim();
        foreach (VersionRowViewModel row in _state.Versions)
        {
            if (Category != "全部" && row.CategoryLabel != Category)
            {
                continue;
            }

            if (keyword.Length > 0 && !row.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            row.Install = InstallSelectedCommand;
            Versions.Add(row);
        }

        LocalInstances.Clear();
        foreach (InstalledInstance instance in _state.Instances)
        {
            LocalInstances.Add(new InstalledInstanceRow(instance, LaunchInstanceCommand, new RelayCommand(() => { }))
            {
                PlayMinutes = _state.GetPlayMinutes(instance.Id),
            });
        }

        OnPropertyChanged(nameof(HasLocalInstances));
    }

    private async Task LaunchSelectedAsync()
    {
        if (_state.SelectedVersion is not { } row)
        {
            _host.ShowToast("请先在右侧选择一个版本");
            _host.ShowPage("download");
            return;
        }

        if (!row.IsInstalled)
        {
            // 未安装时不弹错误，直接引导到安装页面（避免"按钮点了没反应"的困惑）。
            _host.ShowToast($"{row.Id} 尚未安装，已打开安装页面");
            _host.OpenInstallPage(row);
            return;
        }

        await _host.LaunchInstanceAsync(row.Id).ConfigureAwait(true);
    }

    private void InstallSelected()
    {
        if (_state.SelectedVersion is { } row)
        {
            _host.OpenInstallPage(row);
        }
    }

    private async Task RefreshAsync()
    {
        Status = "正在获取版本清单…";
        try
        {
            Status = await VersionLoader.LoadIntoAsync(_services, _state).ConfigureAwait(true);
            ApplyFilter();
            _state.RequestToast(Status);
        }
        catch (Exception ex)
        {
            Status = $"刷新失败：{ex.GetType().Name}";
        }
    }

    /// <summary>页面激活时调用。</summary>
    public void OnActivated()
    {
        ApplyFilter();
        OnPropertyChanged(nameof(HeroTitle));
        OnPropertyChanged(nameof(HeroSubtitle));
    }
}