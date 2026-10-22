using System.Collections.ObjectModel;
using System.Windows.Input;
using ACL.App.Services;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using Avalonia.Threading;
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
            new NavItem("launch", "M4,2.5 L13.5,8 L4,13.5 Z", new LaunchPageViewModel(services, State)),
            new NavItem("download", "M7.2,1.5 H8.8 V7 H11.5 L8,11.5 L4.5,7 H7.2 Z M2.5,12.6 H13.5 V14.4 H2.5 Z", new DownloadPageViewModel(services, State)),
            new NavItem("resources", "M2.5,2.5 H7.5 V7.5 H2.5 Z M8.5,2.5 H13.5 V7.5 H8.5 Z M2.5,8.5 H7.5 V13.5 H2.5 Z M8.5,8.5 H13.5 V13.5 H8.5 Z", new ResourcePageViewModel(services, State)),
            new NavItem("settings", "M2.5,3.6 H13.5 V5.4 H2.5 Z M2.5,7.1 H13.5 V8.9 H2.5 Z M2.5,10.6 H13.5 V12.4 H2.5 Z", new SettingsPageViewModel(services, State)),
            new NavItem("more", "M2.4,6.7 H4.6 V9.3 H2.4 Z M6.9,6.7 H9.1 V9.3 H6.9 Z M11.4,6.7 H13.6 V9.3 H11.4 Z", new MorePageViewModel(services)),
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

        // 同步下载页的筛选列表与状态，避免界面看起来“空”。
        foreach (NavItem item in Pages)
        {
            if (item.Page is DownloadPageViewModel download)
            {
                download.ApplyFilter();
                download.Status = VersionStatus;
            }
        }
    }

    /// <summary>离线登录（占据账号位）。</summary>
    [RelayCommand]
    private void LoginOffline()
    {
        State.AccountStatus = $"离线 · {State.PlayerName}";
        State.DeviceCodeText = string.Empty;
    }

    /// <summary>微软正版登录（设备码流程）。</summary>
    [RelayCommand]
    private async Task LoginMicrosoftAsync()
    {
        if (State.IsAuthenticating)
        {
            return;
        }

        State.IsAuthenticating = true;
        State.AccountStatus = "正在请求设备码…";
        State.DeviceCodeText = string.Empty;
        try
        {
            Result<AuthSession> result = await _services.Auth.LoginMicrosoftAsync((info, _) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    State.DeviceCodeText = $"请在浏览器打开 {info.VerificationUri} 并输入代码：{info.UserCode}";
                    State.AccountStatus = "等待用户在浏览器中授权…";
                });
                return Task.CompletedTask;
            }).ConfigureAwait(true);

            if (result.IsSuccess)
            {
                State.PlayerName = result.Value!.Name;
                State.AccountStatus = $"正版 · {result.Value!.Name}";
                State.DeviceCodeText = string.Empty;
            }
            else
            {
                State.AccountStatus = $"登录失败：{result.Code}";
                State.DeviceCodeText = result.Code == ErrorCode.InvalidAppRegistration
                    ? "该 Azure 应用尚未通过 Mojang 审核，过审后即可正版登录；当前可使用离线登录（详见 docs/09）。"
                    : result.Detail ?? "请稍后重试。";
            }
        }
        finally
        {
            State.IsAuthenticating = false;
        }
    }

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