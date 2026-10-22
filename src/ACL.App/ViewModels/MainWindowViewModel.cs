using System.Collections.ObjectModel;
using System.Windows.Input;
using ACL.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>主窗口视图模型：导航、全屏仪表盘与当前页面。</summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppServices _services;

    /// <summary>构造视图模型（自建服务）。</summary>
    public MainWindowViewModel()
        : this(AppServices.Create())
    {
    }

    /// <summary>构造视图模型（注入服务，便于测试）。</summary>
    public MainWindowViewModel(AppServices services)
    {
        _services = services;
        State = new AppState();
        SelectPageCommand = new RelayCommand<NavItem>(SelectPage);
        ToggleFullscreenCommand = new RelayCommand(() => IsFullscreen = !IsFullscreen);
        ShowPageCommand = new RelayCommand<NavItem>(item =>
        {
            SelectPage(item);
            IsFullscreen = false;
        });

        Pages =
        [
            new NavItem("launch", "▶", new LaunchPageViewModel(services, State)),
            new NavItem("download", "⬇", new DownloadPageViewModel(services, State)),
            new NavItem("settings", "⚙", new SettingsPageViewModel(services, State)),
            new NavItem("more", "⋯", new MorePageViewModel(services)),
        ];

        foreach (NavItem page in Pages)
        {
            page.Select = SelectPageCommand;
        }

        _currentPage = Pages[0].Page;
        Pages[0].IsSelected = true;
    }

    /// <summary>共享设置。</summary>
    public AppState State { get; }

    /// <summary>导航项。</summary>
    public ObservableCollection<NavItem> Pages { get; }

    /// <summary>选择页面的命令。</summary>
    public ICommand SelectPageCommand { get; }

    /// <summary>切换全屏仪表盘。</summary>
    public ICommand ToggleFullscreenCommand { get; }

    /// <summary>从全屏仪表盘进入页面。</summary>
    public ICommand ShowPageCommand { get; }

    /// <summary>当前页面。</summary>
    [ObservableProperty]
    private PageViewModelBase _currentPage;

    /// <summary>是否处于全屏仪表盘。</summary>
    [ObservableProperty]
    private bool _isFullscreen;

    /// <summary>窗口标题。</summary>
    public string Title => "ACL";

    /// <summary>版本清单状态。</summary>
    [ObservableProperty]
    private string _versionStatus = "尚未获取版本清单。";

    /// <summary>刷新版本清单。</summary>
    [RelayCommand]
    private async Task RefreshVersionsAsync()
    {
        VersionStatus = "正在获取版本清单…";
        VersionStatus = await VersionLoader.LoadIntoAsync(_services, State).ConfigureAwait(true);
    }

    /// <summary>离线登录（占据账号位）。</summary>
    [RelayCommand]
    private void LoginOffline() => State.AccountStatus = $"离线 · {State.PlayerName}";

    /// <summary>数据目录（关于信息）。</summary>
    public string DataDirectory => _services.DataDirectory;

    private void SelectPage(NavItem? item)
    {
        if (item is null)
        {
            return;
        }

        foreach (NavItem page in Pages)
        {
            page.IsSelected = ReferenceEquals(page, item);
        }

        CurrentPage = item.Page;
    }
}