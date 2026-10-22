using System.Collections.ObjectModel;
using System.Windows.Input;
using ACL.App.Services;
using ACL.Core.Abstractions;
using ACL.Core.Auth;
using ACL.Core.Domain;
using ACL.Core.Launch;
using ACL.Core.Modpack;
using ACL.Core.Version;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>主窗口视图模型：导航、账号、启动覆盖层与当前页面。</summary>
public partial class MainWindowViewModel : ObservableObject, ILauncherHost
{
    private static readonly string[] LaunchHints =
    [
        "ACL 的游戏目录与启动器同目录，整个文件夹拷走即可用。",
        "每个实例都能单独设置 Java、内存与 JVM 参数。",
        "下载页按正式版 / 快照版 / 预览版 / 远古版分区，不会漏掉任何版本。",
        "整合包可以直接从资源页一键下载并生成可启动实例。",
        "正版登录会自动打开网页并复制设备代码，无需手抄。",
        "Forge / NeoForge 安装会调用官方安装器，产物与手动安装一致。",
    ];

    private readonly AppServices _services;
    private readonly LaunchCoordinator _coordinator;
    private readonly NavItem _downloadNav;
    private readonly NavItem _gamesNav;
    private string? _pendingVersionId;
    private CancellationTokenSource? _loginCancellation;
    private CancellationTokenSource? _launchCancellation;
    private DeviceCodePromptInfo? _lastPrompt;
    private int _hintIndex;

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
        _coordinator = new LaunchCoordinator(services, State);

        SelectPageCommand = new RelayCommand<NavItem>(SelectPage);
        ToggleFullscreenCommand = new RelayCommand(() => IsFullscreen = !IsFullscreen);
        ShowPageCommand = new RelayCommand<string?>(ShowPage);
        RefreshVersionsCommand = new AsyncRelayCommand(RefreshVersionsAsync);
        ActivateAccountCommand = new RelayCommand<AccountRowViewModel>(ActivateAccount);
        DeleteAccountCommand = new RelayCommand<AccountRowViewModel>(DeleteAccount);
        LoginOfflineCommand = new RelayCommand(CreateOfflineAccount);
        LoginMicrosoftCommand = new AsyncRelayCommand(LoginMicrosoftAsync);
        CancelLoginCommand = new RelayCommand(CancelLogin);
        OpenVerificationUrlCommand = new RelayCommand(OpenVerificationUrl);
        CopyDeviceCodeCommand = new AsyncRelayCommand(CopyDeviceCodeAsync);
        CancelLaunchCommand = new RelayCommand(CancelLaunch);
        HideLaunchCommand = new RelayCommand(() => State.IsLaunching = false);
        ImportModpackCommand = new AsyncRelayCommand<string>(ImportModpackAsync);
        AccountPanelCommands accountCommands = new(
            LoginOfflineCommand,
            LoginMicrosoftCommand,
            ActivateAccountCommand,
            DeleteAccountCommand);

        Pages =
        [
            new NavItem("launch", "M4,2.5 L13.5,8 L4,13.5 Z", new LaunchPageViewModel(services, State, this)),
            new NavItem("games", "M2.5,3 H6.5 V6 H2.5 Z M9.5,3 H13.5 V6 H9.5 Z M2.5,10 H6.5 V13 H2.5 Z M9.5,10 H13.5 V13 H9.5 Z", new GameListPageViewModel(services, State, this)),
            new NavItem("download", "M7.2,1.5 H8.8 V7 H11.5 L8,11.5 L4.5,7 H7.2 Z M2.5,12.6 H13.5 V14.4 H2.5 Z", new DownloadPageViewModel(services, State, this)),
            new NavItem("resources", "M2.5,2.5 H7.5 V7.5 H2.5 Z M8.5,2.5 H13.5 V7.5 H8.5 Z M2.5,8.5 H7.5 V13.5 H2.5 Z M8.5,8.5 H13.5 V13.5 H8.5 Z", new ResourcePageViewModel(services, State, this)),
            new NavItem("dashboard", "M2.5,9 H5.5 V13.5 H2.5 Z M6.8,5 H9.8 V13.5 H6.8 Z M11.1,2.5 H14.1 V13.5 H11.1 Z", new DashboardPageViewModel(services, State)),
            new NavItem("settings", "M2.5,3.6 H13.5 V5.4 H2.5 Z M2.5,7.1 H13.5 V8.9 H2.5 Z M2.5,10.6 H13.5 V12.4 H2.5 Z", new SettingsPageViewModel(services, State)),
            new NavItem("more", "M2.4,6.7 H4.6 V9.3 H2.4 Z M6.9,6.7 H9.1 V9.3 H6.9 Z M11.4,6.7 H13.6 V9.3 H11.4 Z", new MorePageViewModel(services)),
        ];

        foreach (NavItem page in Pages)
        {
            page.Select = SelectPageCommand;
        }

        _gamesNav = Pages[1];
        _downloadNav = Pages[2];
        AccountPanel = new AccountPanelViewModel(State, accountCommands);
        LaunchPage = (LaunchPageViewModel)Pages[0].Page;
        GameListPage = (GameListPageViewModel)Pages[1].Page;
        DownloadPage = (DownloadPageViewModel)Pages[2].Page;
        ResourcePage = (ResourcePageViewModel)Pages[3].Page;
        DashboardPage = (DashboardPageViewModel)Pages[4].Page;
        SettingsPage = (SettingsPageViewModel)Pages[5].Page;

        BuildLeftPanels();

        _currentPage = Pages[0].Page;
        Pages[0].IsSelected = true;

        LoadSettings(services);

        // 启动即扫描本机实例，保证"游戏列表 / 本机已安装"在首屏就是真实状态。
        RefreshInstances();
        ActivateCurrentPage();

        State.PlaytimeChanged += (_, _) => SaveSettingsOnExit();
        State.SettingsChanged += (_, _) => SaveSettingsOnExit();
        State.ToastRequested += (_, text) => ShowToast(text);

        App.ApplyTheme(State.Theme);
        ACL.App.Animations.ExpressivePageTransition.ReduceMotion = State.ReduceMotion;
        State.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.Theme))
            {
                App.ApplyTheme(State.Theme);
                SaveSettingsOnExit();
            }

            if (e.PropertyName == nameof(AppState.ReduceMotion))
            {
                ACL.App.Animations.ExpressivePageTransition.ReduceMotion = State.ReduceMotion;
            }

            if (e.PropertyName is nameof(AppState.MaxMemoryMb) or nameof(AppState.MinMemoryMb)
                or nameof(AppState.PlayerName) or nameof(AppState.PreferOfficial) or nameof(AppState.SelectedVersionId))
            {
                SaveSettingsOnExit();
            }
        };
    }
    /// <summary>共享状态。</summary>
    public AppState State { get; }

    /// <summary>账号面板。</summary>
    public AccountPanelViewModel AccountPanel { get; }

    /// <summary>启动页。</summary>
    public LaunchPageViewModel LaunchPage { get; }

    /// <summary>游戏列表页。</summary>
    public GameListPageViewModel GameListPage { get; }

    /// <summary>下载页。</summary>
    public DownloadPageViewModel DownloadPage { get; }

    /// <summary>资源页。</summary>
    public ResourcePageViewModel ResourcePage { get; }

    /// <summary>仪表板页。</summary>
    public DashboardPageViewModel DashboardPage { get; }

    /// <summary>设置页。</summary>
    public SettingsPageViewModel SettingsPage { get; }

    /// <summary>导航项。</summary>
    public ObservableCollection<NavItem> Pages { get; }

    /// <summary>选择页面的命令。</summary>
    public ICommand SelectPageCommand { get; }

    /// <summary>切换全屏仪表盘。</summary>
    public ICommand ToggleFullscreenCommand { get; }

    /// <summary>按导航键进入页面。</summary>
    public ICommand ShowPageCommand { get; }

    /// <summary>刷新版本清单。</summary>
    public ICommand RefreshVersionsCommand { get; }

    /// <summary>激活账号。</summary>
    public ICommand ActivateAccountCommand { get; }

    /// <summary>删除账号。</summary>
    public ICommand DeleteAccountCommand { get; }

    /// <summary>新建离线账号。</summary>
    public ICommand LoginOfflineCommand { get; }

    /// <summary>正版登录（设备码）。</summary>
    public ICommand LoginMicrosoftCommand { get; }

    /// <summary>取消登录。</summary>
    public ICommand CancelLoginCommand { get; }

    /// <summary>打开验证网页。</summary>
    public ICommand OpenVerificationUrlCommand { get; }

    /// <summary>复制设备代码。</summary>
    public ICommand CopyDeviceCodeCommand { get; }

    /// <summary>取消启动。</summary>
    public ICommand CancelLaunchCommand { get; }

    /// <summary>隐藏启动覆盖层（游戏继续运行）。</summary>
    public ICommand HideLaunchCommand { get; }

    /// <summary>整合包导入命令（拖入文件）。</summary>
    public ICommand ImportModpackCommand { get; }

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
    // ===== 对话框 =====

    /// <summary>对话框是否显示。</summary>
    [ObservableProperty]
    private bool _isDialogVisible;

    /// <summary>对话框标题。</summary>
    [ObservableProperty]
    private string _dialogTitle = string.Empty;

    /// <summary>对话框说明。</summary>
    [ObservableProperty]
    private string _dialogMessage = string.Empty;

    /// <summary>对话框中的设备代码。</summary>
    [ObservableProperty]
    private string _dialogCode = string.Empty;

    /// <summary>对话框是否为错误提示（隐藏设备码与网页按钮）。</summary>
    [ObservableProperty]
    private bool _dialogIsError;

    /// <summary>轻提示文本。</summary>
    [ObservableProperty]
    private string _toast = string.Empty;

    /// <summary>整合包导入状态。</summary>
    [ObservableProperty]
    private string _importStatus = string.Empty;

    // ===== 导航 =====

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
        ActivateCurrentPage();
    }

    /// <summary>按导航键切换页面。</summary>
    public void ShowPage(string? navKey)
    {
        if (string.IsNullOrEmpty(navKey))
        {
            return;
        }

        NavItem? item = Pages.FirstOrDefault(page => string.Equals(page.Key, navKey, StringComparison.Ordinal));
        if (item is not null)
        {
            SelectPage(item);
        }
    }

    private void ActivateCurrentPage()
    {
        switch (CurrentPage)
        {
            case GameListPageViewModel games:
                games.OnActivated();
                break;
            case DownloadPageViewModel download:
                download.OnActivated();
                break;
            case LaunchPageViewModel launch:
                launch.OnActivated();
                break;
            case DashboardPageViewModel dashboard:
                dashboard.Refresh();
                break;
        }

        UpdateLeftPanels();
    }

    // ===== ILauncherHost =====

    /// <inheritdoc />
    void ILauncherHost.ShowToast(string text) => ShowToast(text);

    /// <inheritdoc />
    void ILauncherHost.ShowPage(string navKey) => ShowPage(navKey);

    /// <inheritdoc />
    public void RefreshInstances()
    {
        GameListPage.Refresh();
        LaunchPage.ApplyFilter();
        DownloadPage.ApplyFilter();
        DashboardPage.Refresh();
        UpdateLeftPanels();
    }

    /// <inheritdoc />
    public void OpenInstallPage(VersionRowViewModel row)
    {
        InstallPageViewModel page = new(_services, State, this, row);
        page.LeftPanel = BuildInstallLeftPanel(page);
        foreach (NavItem item in Pages)
        {
            item.IsSelected = ReferenceEquals(item, _downloadNav);
        }

        CurrentPage = page;
        UpdateLeftPanels();
    }

    /// <inheritdoc />
    public void OpenInstanceSettings(InstalledInstanceRow row)
    {
        InstanceSettingsPageViewModel page = new(_services, State, this, row.Instance);
        page.LeftPanel = new LeftPanelViewModel("实例设置", "只影响该实例；留空即沿用全局设置")
            .Set("实例", row.Instance.Id)
            .Set("加载器", row.Instance.LoaderLabel)
            .Set("父版本", row.Instance.InheritsFrom ?? "—")
            .Add("启动该实例", page.LaunchCommand)
            .Add("返回列表", page.BackCommand);

        foreach (NavItem item in Pages)
        {
            item.IsSelected = ReferenceEquals(item, _gamesNav);
        }

        CurrentPage = page;
        UpdateLeftPanels();
    }
    // ===== 启动 =====

    /// <inheritdoc />
    public async Task LaunchInstanceAsync(string instanceId)
    {
        if (State.IsLaunching)
        {
            ShowToast("已有游戏正在启动 / 运行中");
            return;
        }

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            ShowToast("请先选择一个版本");
            return;
        }

        State.LaunchVersionId = instanceId;
        State.LaunchTitle = "正在启动游戏";
        State.LaunchStage = "初始化";
        State.LaunchProgress = 0;
        State.LaunchSpeed = string.Empty;
        State.LaunchMethod = State.CurrentAccount is { } account
            ? (account.Type == AccountType.Offline ? "离线登录" : "正版登录")
            : "离线登录";
        State.LaunchHint = NextHint();
        State.LaunchStartedAt = DateTimeOffset.UtcNow;
        State.CanCancelLaunch = true;
        State.IsLaunching = true;

        _launchCancellation = new CancellationTokenSource();
        CancellationToken token = _launchCancellation.Token;
        try
        {
            UiProgressSink sink = new(report =>
            {
                State.LaunchProgress = report.Fraction;
                State.LaunchStage = string.IsNullOrWhiteSpace(report.Stage) ? State.LaunchStage : report.Stage;
                if (report.Message is { Length: > 0 } message && message.EndsWith("/s", StringComparison.Ordinal))
                {
                    State.LaunchSpeed = message;
                }
            });

            Result<GameExitInfo> result = await Task.Run(
                () => _coordinator.LaunchAsync(instanceId, sink, token), token).ConfigureAwait(true);

            if (result.IsSuccess)
            {
                GameExitInfo exit = result.Value!;
                State.LaunchProgress = 1;
                State.LaunchStage = "游戏已退出";
                ShowToast($"游戏已退出：{exit.State}（本次 {exit.Duration.TotalMinutes:F1} 分钟）");
                PersistLog(exit.LogTail);
                RefreshInstances();
            }
            else if (result.Code == ErrorCode.Canceled)
            {
                ShowToast("已取消启动");
            }
            else
            {
                DialogTitle = "启动失败";
                DialogCode = string.Empty;
                DialogIsError = true;
                DialogMessage = $"{DescribeLaunchError(result.Code)}\n\n{result.Detail}";
                IsDialogVisible = true;
            }
        }
        catch (OperationCanceledException)
        {
            ShowToast("已取消启动");
        }
        catch (Exception ex)
        {
            DialogTitle = "启动失败";
            DialogCode = string.Empty;
            DialogIsError = true;
            DialogMessage = ex.Message;
            IsDialogVisible = true;
        }
        finally
        {
            State.IsLaunching = false;
            State.CanCancelLaunch = false;
            try
            {
                _launchCancellation?.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            _launchCancellation = null;
        }
    }

    private static string DescribeLaunchError(ErrorCode code) => code switch
    {
        ErrorCode.JavaMissing => "未找到合适的 Java 运行时。",
        ErrorCode.JavaIncompatible => "Java 版本与目标游戏不兼容。",
        ErrorCode.NotFound => "未找到该实例的版本文件（可能已被移动或删除）。",
        ErrorCode.ProcessStartFailed => "无法启动 Java 进程。",
        _ => "启动过程出错。",
    };

    /// <summary>取消启动（会终止本次启动流程）。</summary>
    private void CancelLaunch()
    {
        try
        {
            _launchCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        State.CanCancelLaunch = false;
    }

    private string NextHint()
    {
        string hint = LaunchHints[_hintIndex % LaunchHints.Length];
        _hintIndex++;
        return hint;
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
            State.LastLogPath = path;
        }
        catch (IOException)
        {
        }
    }
    // ===== 账号 =====

    /// <summary>新建离线账号。</summary>
    private void CreateOfflineAccount()
    {
        string name = AccountPanel.NewOfflineName.Trim();
        if (name.Length == 0)
        {
            ShowToast("请输入离线账号名");
            return;
        }

        if (name.Length > 16)
        {
            name = name[..16];
        }

        AccountProfile profile = AccountProfile.Offline(name, DateTimeOffset.UtcNow);
        Result saved = _services.Accounts.Save(profile, OfflineIdentity.CreateSession(name));
        if (!saved.IsSuccess)
        {
            ShowToast($"创建离线账号失败：{saved.Code}");
            return;
        }

        LoadAccounts(profile.Id);
        AccountPanel.NewOfflineName = string.Empty;
        ShowToast($"已创建离线账号 {name}");
        SaveSettingsOnExit();
    }

    private void ActivateAccount(AccountRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        AccountProfile profile = row.Profile with { LastUsedAt = DateTimeOffset.UtcNow };
        Result<AuthSession> session = _services.Accounts.LoadSession(profile.Id);
        AuthSession? authSession = session.IsSuccess ? session.Value : null;
        if (authSession is null && profile.Type == AccountType.Offline)
        {
            authSession = OfflineIdentity.CreateSession(profile.Name);
            _services.Accounts.Save(profile, authSession);
        }
        else
        {
            _services.Accounts.Save(profile);
        }

        State.ActivateAccount(profile, authSession);
        LoadAccounts(profile.Id);
        ShowToast($"已切换到 {profile.Name}");
        SaveSettingsOnExit();
    }

    private void DeleteAccount(AccountRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        Result deleted = _services.Accounts.Delete(row.Profile.Id);
        if (!deleted.IsSuccess)
        {
            ShowToast($"删除失败：{deleted.Code}");
            return;
        }

        bool wasCurrent = State.CurrentAccount is { } current && string.Equals(current.Id, row.Profile.Id, StringComparison.Ordinal);
        LoadAccounts(wasCurrent ? null : State.CurrentAccount?.Id);
        ShowToast($"已删除账号 {row.Profile.Name}");
    }

    /// <summary>读取本地账号列表并按需激活。</summary>
    private void LoadAccounts(string? activateId)
    {
        State.Accounts.Clear();
        Result<IReadOnlyList<AccountProfile>> listed = _services.Accounts.List();
        if (listed.IsSuccess)
        {
            foreach (AccountProfile profile in listed.Value!)
            {
                State.Accounts.Add(profile);
            }
        }

        AccountProfile? target = activateId is { Length: > 0 }
            ? State.Accounts.FirstOrDefault(a => string.Equals(a.Id, activateId, StringComparison.Ordinal))
            : State.Accounts.FirstOrDefault();

        if (target is not null)
        {
            AccountProfile profile = target;
            Result<AuthSession> session = _services.Accounts.LoadSession(profile.Id);
            AuthSession? authSession = session.IsSuccess ? session.Value : null;
            if (authSession is null && profile.Type == AccountType.Offline)
            {
                authSession = OfflineIdentity.CreateSession(profile.Name);
            }

            State.ActivateAccount(profile, authSession);
        }
        else
        {
            State.CurrentAccount = null;
            State.CurrentSession = null;
            State.AccountStatus = "未登录（离线启动将使用下方玩家名）";
        }

        State.RaiseAccountsChanged();
    }
    /// <summary>正版登录（设备码流程）。</summary>
    private async Task LoginMicrosoftAsync()
    {
        if (State.IsAuthenticating)
        {
            return;
        }

        State.IsAuthenticating = true;
        State.AccountStatus = "正在请求设备码…";
        State.DeviceCodeText = string.Empty;
        DialogIsError = false;
        _loginCancellation = new CancellationTokenSource();
        try
        {
            CancellationToken loginToken = _loginCancellation.Token;
            Result<AuthSession> result = await Task.Run(
                async () => await _services.Auth.LoginMicrosoftAsync(
                    (info, promptToken) =>
                    {
                        _lastPrompt = info;
                        Dispatcher.UIThread.Post(() =>
                        {
                            DialogTitle = "登录 Minecraft";
                            DialogCode = info.UserCode;
                            DialogIsError = false;
                            DialogMessage =
                                "已自动打开登录网页，请在网页中输入上方代码（代码已复制到剪贴板）。\n\n" +
                                $"若页面打不开，可用其他设备访问 {info.VerificationUri} 手动输入代码。";
                            IsDialogVisible = true;
                            State.AccountStatus = "等待浏览器中的授权…";
                        });

                        string url = info.VerificationUriComplete ?? info.VerificationUri;
                        _ = Task.Run(() => OpenUrl(url));
                        Dispatcher.UIThread.Post(() => _ = CopyToClipboardAsync(info.UserCode));
                        return Task.CompletedTask;
                    },
                    loginToken)).ConfigureAwait(true);

            if (result.IsSuccess)
            {
                AuthSession session = result.Value!;
                AccountProfile profile = AccountProfile.FromSession(session, DateTimeOffset.UtcNow);
                _services.Accounts.Save(profile, session);
                LoadAccounts(profile.Id);
                ShowToast($"正版登录成功：{session.Name}");
            }
            else if (result.Code == ErrorCode.Canceled)
            {
                State.AccountStatus = "已取消登录";
            }
            else
            {
                State.AccountStatus = $"登录失败：{result.Code}";
                DialogTitle = "登录失败";
                DialogCode = string.Empty;
                DialogIsError = true;
                DialogMessage = result.Code == ErrorCode.InvalidAppRegistration
                    ? "该 Azure 应用尚未通过 Mojang 审核；过审后即可正版登录。当前可先使用离线登录。"
                    : result.Detail ?? "请稍后重试。";
                IsDialogVisible = true;
            }
        }
        catch (OperationCanceledException)
        {
            State.AccountStatus = "已取消登录";
            ShowToast("已取消登录");
        }
        catch (Exception ex)
        {
            State.AccountStatus = $"登录失败：{ex.GetType().Name}";
            DialogTitle = "登录失败";
            DialogCode = string.Empty;
            DialogIsError = true;
            DialogMessage = ex.Message;
            IsDialogVisible = true;
        }
        finally
        {
            IsDialogVisible = false;
            State.IsAuthenticating = false;
            try
            {
                _loginCancellation?.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            _loginCancellation = null;
        }
    }

    private void OpenVerificationUrl()
    {
        if (_lastPrompt is { } prompt)
        {
            OpenUrl(prompt.VerificationUriComplete ?? prompt.VerificationUri);
        }
    }

    private async Task CopyDeviceCodeAsync()
    {
        if (_lastPrompt is { } prompt)
        {
            await CopyToClipboardAsync(prompt.UserCode);
        }
    }

    private void CancelLogin()
    {
        try
        {
            _loginCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        IsDialogVisible = false;
    }
    /// <summary>刷新版本清单。</summary>
    private async Task RefreshVersionsAsync()
    {
        VersionStatus = "正在获取版本清单…";
        try
        {
            VersionStatus = await VersionLoader.LoadIntoAsync(_services, State).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            VersionStatus = $"获取版本清单失败：{ex.GetType().Name}";
            return;
        }

        if (_pendingVersionId is { Length: > 0 } pending)
        {
            State.SelectedVersion = State.Versions.FirstOrDefault(v => v.Id == pending) ?? State.SelectedVersion;
            _pendingVersionId = null;
        }

        DownloadPage.ApplyFilter();
        DownloadPage.Status = VersionStatus;
        LaunchPage.ApplyFilter();
        LaunchPage.Status = VersionStatus;
        DashboardPage.Refresh();
        RefreshInstances();
        UpdateLeftPanels();
    }

    /// <summary>整合包导入（拖入 .mrpack / .zip）。</summary>
    public async Task ImportModpackAsync(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        ImportStatus = $"正在解析整合包：{Path.GetFileName(path)}…";
        Result<ModpackManifest> parsed;
        try
        {
            parsed = _services.Modpack.Parse(path);
        }
        catch (Exception ex)
        {
            ImportStatus = $"解析异常：{ex.GetType().Name}";
            return;
        }

        if (!parsed.IsSuccess)
        {
            ImportStatus = $"解析失败：{parsed.Code}（{parsed.Detail}）";
            return;
        }

        ModpackManifest manifest = parsed.Value!;
        ImportStatus = $"{manifest.Name}（{manifest.Format}）：Minecraft {manifest.MinecraftVersion}，文件 {manifest.Files.Count} 个，开始安装…";

        Result<ModpackInstallResult> installed;
        try
        {
            installed = await _services.Modpack
                .InstallAsync(manifest, path, _services.GameDirectory)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ImportStatus = $"导入异常：{ex.GetType().Name}";
            return;
        }

        ImportStatus = installed.IsSuccess
            ? $"已导入 {manifest.Name}：覆盖文件 {installed.Value!.OverridesCopied} 个，下载 {installed.Value!.Downloaded.Count} 个。"
            : $"导入失败：{installed.Code}（{installed.Detail}）";
        ShowToast(ImportStatus);
        RefreshInstances();
    }

    // ===== 设置持久化 =====

    private void LoadSettings(AppServices services)
    {
        Result<AclSettings> loaded = services.Settings.Load();
        if (!loaded.IsSuccess)
        {
            LoadAccounts(null);
            return;
        }

        AclSettings settings = loaded.Value!;
        State.MinMemoryMb = settings.MinMemoryMb;
        State.MaxMemoryMb = settings.MaxMemoryMb;
        State.PreferOfficial = settings.PreferOfficialMirror;
        State.PlayerName = settings.PlayerName;
        _pendingVersionId = settings.SelectedVersionId;
        State.Theme = settings.Theme;
        State.ReduceMotion = settings.ReduceMotion;
        foreach (KeyValuePair<string, double> play in settings.PlayMinutes)
        {
            State.PlayMinutes[play.Key] = play.Value;
        }

        foreach (KeyValuePair<string, InstanceSettings> instance in settings.Instances)
        {
            State.InstanceSettings[instance.Key] = instance.Value;
        }

        LoadAccounts(settings.CurrentAccountId);
    }

    /// <summary>关闭前 / 变更后保存设置。</summary>
    public void SaveSettingsOnExit()
    {
        AclSettings settings = new()
        {
            SchemaVersion = 2,
            MinMemoryMb = (int)State.MinMemoryMb,
            MaxMemoryMb = (int)State.MaxMemoryMb,
            PreferOfficialMirror = State.PreferOfficial,
            PlayerName = State.PlayerName,
            SelectedVersionId = State.SelectedVersionId,
            CurrentAccountId = State.CurrentAccount?.Id,
            Theme = State.Theme,
            ReduceMotion = State.ReduceMotion,
            PlayMinutes = new Dictionary<string, double>(State.PlayMinutes, StringComparer.Ordinal),
            Instances = new Dictionary<string, InstanceSettings>(State.InstanceSettings, StringComparer.Ordinal),
        };

        Result saved = _services.Settings.Save(settings);
        SettingsPage.SettingsStatus = saved.IsSuccess ? "设置已保存" : $"保存失败：{saved.Code}";
    }
    // ===== 左栏 =====

    private void BuildLeftPanels()
    {
        Pages[0].Page.LeftPanel = AccountPanel;

        Pages[1].Page.LeftPanel = new LeftPanelViewModel("本机游戏", "已安装实例与实例级设置")
            .Set("实例数", "—")
            .Set("累计游玩", "—")
            .Add("重新扫描", GameListPage.RescanCommand)
            .Add("安装新版本", ShowPageCommand, "download");

        Pages[2].Page.LeftPanel = new LeftPanelViewModel("安装与下载", "全部版本 + 加载器前置安装")
            .Set("目标目录", _services.GameDirectory)
            .Set("版本总数", "—")
            .Set("已安装", "—")
            .Add("刷新版本清单", RefreshVersionsCommand)
            .Add("进入游戏列表", ShowPageCommand, "games");

        Pages[3].Page.LeftPanel = new LeftPanelViewModel("检索设置", "Modrinth 免鉴权；CurseForge 需 API Key")
            .Set("来源站", "Modrinth")
            .Set("结果", "—")
            .Set("推荐", "—")
            .Set("安装位置", "mods · resourcepacks · shaderpacks · datapacks")
            .Set("密钥", _services.HasCurseForgeKey ? "CurseForge 已配置" : "CurseForge 未配置")
            .Add("加载推荐资源", ResourcePage.LoadRecommendedCommand);

        Pages[4].Page.LeftPanel = new LeftPanelViewModel("运行环境", "内存 / 主题 / Java / 日志")
            .Set("内存", "—")
            .Set("主题", "—")
            .Set("Java", "—")
            .Add("刷新统计", DashboardPage.RefreshCommand)
            .Add("打开日志目录", DashboardPage.OpenLogDirectoryCommand);

        Pages[5].Page.LeftPanel = new LeftPanelViewModel("外观与目录", "主题 / 动效 / 路径")
            .Set("主题", "—")
            .Set("动效", "—")
            .Set("数据目录", _services.DataDirectory)
            .Add("深色", SettingsPage.UseDarkThemeCommand)
            .Add("浅色", SettingsPage.UseLightThemeCommand)
            .Add("保存设置", SettingsPage.SaveSettingsCommand);

        Pages[6].Page.LeftPanel = new LeftPanelViewModel("ACL", "完全自研的 Minecraft 启动器")
            .Set("版本", "0.2.0（自研 UI-γ）")
            .Set("内核", "版本 / 下载 / 认证 / Java / 启动 / 资源 / 整合包")
            .Set("合规", "净室自研：不含任何第三方启动器源码")
            .Add("打开数据目录", ((MorePageViewModel)Pages[6].Page).OpenDataDirectoryCommand);

        ResourcePage.ResultsChanged += (_, _) => UpdateLeftPanels();
        State.PlaytimeChanged += (_, _) => UpdateLeftPanels();
        UpdateLeftPanels();
    }

    private LeftPanelViewModel BuildInstallLeftPanel(InstallPageViewModel page)
        => new LeftPanelViewModel($"安装 {page.Version.Id}", "选择加载器并安装到本机游戏目录")
            .Set("目标版本", page.Version.Id)
            .Set("分类", page.Version.CategoryLabel)
            .Set("加载器", "原版")
            .Set("实例名", page.InstanceName)
            .Set("Java", "未探测")
            .Add("返回下载页", page.BackCommand)
            .Add("探测 Java", page.DetectJavaCommand);

    private void UpdateLeftPanels()
    {
        int versionCount = State.Versions.Count;
        int installed = State.Instances.Count;
        double minutes = State.PlayMinutes.Values.Sum();
        int results = ResourcePage.Results.Count;

        SetLeftField(Pages[1].Page, "实例数", installed.ToString());
        SetLeftField(Pages[1].Page, "累计游玩", minutes <= 0 ? "—" : $"{minutes:F1} 分钟");
        SetLeftField(Pages[2].Page, "版本总数", versionCount.ToString());
        SetLeftField(Pages[2].Page, "已安装", installed.ToString());
        SetLeftField(Pages[3].Page, "结果", results.ToString());
        SetLeftField(Pages[3].Page, "推荐", ResourcePage.Recommended.Count.ToString());
        SetLeftField(Pages[4].Page, "内存", $"{(int)State.MinMemoryMb} - {(int)State.MaxMemoryMb} MB");
        SetLeftField(Pages[4].Page, "主题", State.Theme == "light" ? "浅色" : "深色");
        SetLeftField(Pages[4].Page, "Java", string.IsNullOrEmpty(State.SelectedJavaHome) ? "尚未探测" : "已选择");
        SetLeftField(Pages[5].Page, "主题", State.Theme == "light" ? "浅色" : "深色");
        SetLeftField(Pages[5].Page, "动效", State.ReduceMotion ? "已减弱" : "完整");

        if (CurrentPage is InstallPageViewModel install && install.LeftPanel is LeftPanelViewModel installPanel)
        {
            installPanel.Set("加载器", install.LoaderKind);
            installPanel.Set("实例名", install.InstanceName);
            installPanel.Set("Java", string.IsNullOrEmpty(install.SelectedJavaHome) ? "未探测" : install.SelectedJavaHome!);
        }
    }

    private static void SetLeftField(PageViewModelBase? page, string label, string value)
    {
        if (page?.LeftPanel is LeftPanelViewModel panel)
        {
            panel.Set(label, value);
        }
    }
    // ===== 轻提示 / 辅助 =====

    private Avalonia.Threading.DispatcherTimer? _toastTimer;

    /// <summary>显示一条轻提示。</summary>
    public void ShowToast(string text)
    {
        Toast = text;
        _toastTimer ??= new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _toastTimer.Stop();
        _toastTimer.Tick -= OnToastTick;
        _toastTimer.Tick += OnToastTick;
        _toastTimer.Start();
    }

    private void OnToastTick(object? sender, EventArgs e)
    {
        _toastTimer?.Stop();
        Toast = string.Empty;
    }

    /// <summary>数据目录（关于信息）。</summary>
    public string DataDirectory => _services.DataDirectory;

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    private async Task CopyToClipboardAsync(string text)
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is { } window
                && TopLevel.GetTopLevel(window)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text).WaitAsync(TimeSpan.FromSeconds(2));
                ShowToast("设备代码已复制到剪贴板");
            }
        }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            ShowToast("设备代码复制失败，请手动复制");
        }
    }
}