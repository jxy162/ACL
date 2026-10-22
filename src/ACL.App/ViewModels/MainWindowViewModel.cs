using System.Collections.ObjectModel;
using System.Windows.Input;
using ACL.App.Services;
using ACL.Core.Modpack;
using ACL.Core.Abstractions;
using ACL.Core.Auth;
using ACL.Core.Domain;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>主窗口视图模型：导航、全屏仪表盘与当前页面。</summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string? _pendingVersionId;
    private CancellationTokenSource? _loginCancellation;
    private DeviceCodePromptInfo? _lastPrompt;

    /// <summary>对话框是否显示。</summary>
    [ObservableProperty]
    private bool _isDialogVisible;

    /// <summary>对话框标题。</summary>
    [ObservableProperty]
    private string _dialogTitle = string.Empty;

    /// <summary>对话框说明。</summary>
    [ObservableProperty]
    private string _dialogMessage = string.Empty;

    /// <summary>对话框中的设备代码（可空）。</summary>
    [ObservableProperty]
    private string _dialogCode = string.Empty;

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
            new NavItem("games", "M2.5,3 H6.5 V6 H2.5 Z M9.5,3 H13.5 V6 H9.5 Z M2.5,10 H6.5 V13 H2.5 Z M9.5,10 H13.5 V13 H9.5 Z", new GameListPageViewModel(services, State, StartVersion)),
            new NavItem("download", "M7.2,1.5 H8.8 V7 H11.5 L8,11.5 L4.5,7 H7.2 Z M2.5,12.6 H13.5 V14.4 H2.5 Z", new DownloadPageViewModel(services, State)),
            new NavItem("resources", "M2.5,2.5 H7.5 V7.5 H2.5 Z M8.5,2.5 H13.5 V7.5 H8.5 Z M2.5,8.5 H7.5 V13.5 H2.5 Z M8.5,8.5 H13.5 V13.5 H8.5 Z", new ResourcePageViewModel(services, State)),
            new NavItem("dashboard", "M2.5,9 H5.5 V13.5 H2.5 Z M6.8,5 H9.8 V13.5 H6.8 Z M11.1,2.5 H14.1 V13.5 H11.1 Z", new DashboardPageViewModel(services, State)),
            new NavItem("settings", "M2.5,3.6 H13.5 V5.4 H2.5 Z M2.5,7.1 H13.5 V8.9 H2.5 Z M2.5,10.6 H13.5 V12.4 H2.5 Z", new SettingsPageViewModel(services, State)),
            new NavItem("more", "M2.4,6.7 H4.6 V9.3 H2.4 Z M6.9,6.7 H9.1 V9.3 H6.9 Z M11.4,6.7 H13.6 V9.3 H11.4 Z", new MorePageViewModel(services)),
        ];

        foreach (NavItem page in Pages)
        {
            page.Select = SelectPageCommand;
        }

        // 每页独立的左栏（启动页为可交互账号卡，其余为页面专属信息 + 操作）。
        BuildLeftPanels();

        _currentPage = Pages[0].Page;
        Pages[0].IsSelected = true;

        // 恢复上次的便携设置。
        Result<AclSettings> loaded = services.Settings.Load();
        if (loaded.IsSuccess)
        {
            State.MinMemoryMb = loaded.Value!.MinMemoryMb;
            State.MaxMemoryMb = loaded.Value!.MaxMemoryMb;
            State.PreferOfficial = loaded.Value!.PreferOfficialMirror;
            State.PlayerName = loaded.Value!.PlayerName;
            _pendingVersionId = loaded.Value!.SelectedVersionId;
            State.Theme = loaded.Value!.Theme;
            State.ReduceMotion = loaded.Value!.ReduceMotion;
            foreach (KeyValuePair<string, double> play in loaded.Value!.PlayMinutes)
            {
                State.PlayMinutes[play.Key] = play.Value;
            }
        }

        State.PlaytimeChanged += (_, _) => SaveSettingsOnExit();
        State.ToastRequested += (_, text) => ShowToast(text);

        // 主题与动效：立即应用到界面。
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
                SaveSettingsOnExit();
            }

            if (e.PropertyName is nameof(AppState.Theme) or nameof(AppState.MinMemoryMb)
                or nameof(AppState.MaxMemoryMb) or nameof(AppState.SelectedJavaHome))
            {
                UpdateLeftPanels();
            }
        };
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
        try
        {
            VersionStatus = await VersionLoader.LoadIntoAsync(_services, State).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            VersionStatus = $"获取版本清单失败：{ex.GetType().Name}";
            return;
        }

        // 恢复上次选择的版本。
        if (_pendingVersionId is { Length: > 0 } pending)
        {
            State.SelectedVersion = State.Versions.FirstOrDefault(v => v.Id == pending) ?? State.SelectedVersion;
            _pendingVersionId = null;
        }

        // 同步下载页的筛选列表与状态，避免界面看起来“空”。
        foreach (NavItem item in Pages)
        {
            if (item.Page is DownloadPageViewModel download)
            {
                download.ApplyFilter();
                download.Status = VersionStatus;
            }

            if (item.Page is GameListPageViewModel games)
            {
                games.ApplyFilter();
            }

            if (item.Page is DashboardPageViewModel dashboard)
            {
                dashboard.Refresh();
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

    /// <summary>正版登录（设备码流程）：自动打开浏览器、自动复制代码，并弹出提示框。</summary>
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
        _loginCancellation = new CancellationTokenSource();
        try
        {
            // 整个登录（含网络轮询）都在后台线程执行；UI 只在需要时被派发更新，避免界面卡死。
            CancellationToken loginToken = _loginCancellation.Token;
            Result<AuthSession> result = await Task.Run(
                async () => await _services.Auth.LoginMicrosoftAsync(
                    (info, promptToken) =>
                    {
                        _lastPrompt = info;

                        // 1) 只做 UI 状态更新（快速、非阻塞）。
                        Dispatcher.UIThread.Post(() =>
                        {
                            DialogTitle = "登录 Minecraft";
                            DialogCode = info.UserCode;
                            DialogMessage =
                                "登录网页将自动开启，请在网页中输入上方代码（已自动复制）。\n\n" +
                                "如果网页一直加载不出来，请稍后重试或改善网络环境（加速器 / VPN）；" +
                                $"也可以用其他设备打开 {info.VerificationUri} 并输入上述代码。";
                            IsDialogVisible = true;
                            State.AccountStatus = "等待用户在浏览器中授权…";
                        });

                        // 2) 副作用（唤起浏览器、写剪贴板）放到后台，绝不阻塞 UI 线程。
                        string url = info.VerificationUriComplete ?? info.VerificationUri;
                        _ = Task.Run(() => OpenUrl(url));
                        Dispatcher.UIThread.Post(() => _ = CopyToClipboardAsync(info.UserCode));

                        return Task.CompletedTask;
                    },
                    loginToken)).ConfigureAwait(true);

            if (result.IsSuccess)
            {
                State.PlayerName = result.Value!.Name;
                State.AccountStatus = $"正版 · {result.Value!.Name}";
                State.DeviceCodeText = string.Empty;
                ShowToast($"正版登录成功：{result.Value!.Name}");
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
                DialogMessage = result.Code == ErrorCode.InvalidAppRegistration
                    ? "该 Azure 应用尚未通过 Mojang 审核，过审后即可正版登录；当前可使用离线登录（详见 docs/09）。"
                    : result.Detail ?? "请稍后重试。";
                IsDialogVisible = true;
            }
        }
        catch (OperationCanceledException)
        {
            // 取消是正常控制流：不得让异常逃逸（否则 AsyncRelayCommand 会重新抛出并终止进程）。
            State.AccountStatus = "已取消登录";
            ShowToast("已取消登录");
        }
        catch (Exception ex)
        {
            State.AccountStatus = $"登录失败：{ex.GetType().Name}";
            DialogTitle = "登录失败";
            DialogCode = string.Empty;
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
                // 忽略重复释放。
            }

            _loginCancellation = null;
        }
    }

    /// <summary>打开验证网页。</summary>
    [RelayCommand]
    private void OpenVerificationUrl()
    {
        if (_lastPrompt is { } prompt)
        {
            OpenUrl(prompt.VerificationUriComplete ?? prompt.VerificationUri);
        }
    }

    /// <summary>复制设备代码。</summary>
    [RelayCommand]
    private async Task CopyDeviceCodeAsync()
    {
        if (_lastPrompt is { } prompt)
        {
            await CopyToClipboardAsync(prompt.UserCode);
        }
    }

    /// <summary>取消登录并关闭对话框。</summary>
    [RelayCommand]
    private void CancelLogin()
    {
        try
        {
            _loginCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 已结束的登录：忽略。
        }

        IsDialogVisible = false;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // 打不开浏览器时不影响后续手动操作。
        }
    }

    /// <summary>写剪贴板：带超时保护且失败静默（OLE 剪贴板可能被其他进程占用）。</summary>
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

    /// <summary>轻提示文本（自动消失）。</summary>
    [ObservableProperty]
    private string _toast = string.Empty;

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

    /// <summary>整合包导入状态（拖入文件后显示）。</summary>

    [ObservableProperty]
    private string _importStatus = string.Empty;

    /// <summary>为每个页面构建专属左栏。</summary>
    private void BuildLeftPanels()
    {
        Pages[0].Page.LeftPanel = new AccountLeftPanelViewModel(State);

        Pages[1].Page.LeftPanel = new LeftPanelViewModel("本机版本", "安装状态与游玩时长")
            .Set("版本总数", "—")
            .Set("已安装", "—")
            .Set("累计游玩", "—")
            .Add("刷新版本清单", RefreshVersionsCommand)
            .Add("回到主页", ShowPageCommand, Pages[0]);

        Pages[2].Page.LeftPanel = new LeftPanelViewModel("安装", "把客户端 / 库 / 资源索引补齐到游戏目录")
            .Set("目标目录", _services.GameDirectory)
            .Set("可选版本", "—")
            .Add("刷新版本清单", RefreshVersionsCommand);

        Pages[3].Page.LeftPanel = new LeftPanelViewModel("检索设置", "Modrinth 免鉴权；CurseForge 需 API Key")
            .Set("来源站", "Modrinth")
            .Set("结果", "—")
            .Set("安装位置", "mods · resourcepacks · shaderpacks · datapacks")
            .Set("密钥", _services.HasCurseForgeKey ? "CurseForge 已配置" : "CurseForge 未配置");

        Pages[4].Page.LeftPanel = new LeftPanelViewModel("运行环境", "内存 / 主题 / Java / 日志")
            .Set("内存", "—")
            .Set("主题", "—")
            .Set("Java", "—")
            .Add("刷新统计", ((DashboardPageViewModel)Pages[4].Page).RefreshCommand)
            .Add("打开日志目录", ((DashboardPageViewModel)Pages[4].Page).OpenLogDirectoryCommand);

        Pages[5].Page.LeftPanel = new LeftPanelViewModel("外观与目录", "主题 / 动效 / 路径")
            .Set("主题", "—")
            .Set("动效", "—")
            .Set("数据目录", _services.DataDirectory)
            .Add("深色", ((SettingsPageViewModel)Pages[5].Page).UseDarkThemeCommand)
            .Add("浅色", ((SettingsPageViewModel)Pages[5].Page).UseLightThemeCommand)
            .Add("保存设置", ((SettingsPageViewModel)Pages[5].Page).SaveSettingsCommand);

        Pages[6].Page.LeftPanel = new LeftPanelViewModel("ACL", "完全自研的 Minecraft 启动器")
            .Set("版本", "0.1.0（Kernel-β+）")
            .Set("内核", "版本 / 下载 / 认证 / Java / 启动 / 资源 / 整合包")
            .Set("合规", "净室自研：不含任何第三方启动器源码")
            .Add("打开数据目录", ((MorePageViewModel)Pages[6].Page).OpenDataDirectoryCommand);

        if (Pages[3].Page is ResourcePageViewModel resourcePage)
        {
            resourcePage.ResultsChanged += (_, _) => UpdateLeftPanels();
        }

        State.PlaytimeChanged += (_, _) => UpdateLeftPanels();
        UpdateLeftPanels();
    }

    /// <summary>刷新各页左栏中会变化的数据。</summary>
    private void UpdateLeftPanels()
    {
        int versionCount = State.Versions.Count;
        int installed = 0;
        foreach (VersionRowViewModel row in State.Versions)
        {
            if (_services.FileSystem.FileExists(Path.Combine(_services.GameDirectory, "versions", row.Id, row.Id + ".jar")))
            {
                installed++;
            }
        }

        double minutes = State.PlayMinutes.Values.Sum();
        int results = Pages[3].Page is ResourcePageViewModel rp ? rp.Results.Count : 0;

        SetLeftField(Pages[1].Page, "版本总数", versionCount.ToString());
        SetLeftField(Pages[1].Page, "已安装", installed.ToString());
        SetLeftField(Pages[1].Page, "累计游玩", minutes <= 0 ? "—" : $"{minutes:F1} 分钟");
        SetLeftField(Pages[2].Page, "可选版本", versionCount.ToString());
        SetLeftField(Pages[3].Page, "结果", results.ToString());
        SetLeftField(Pages[4].Page, "内存", $"{(int)State.MinMemoryMb} - {(int)State.MaxMemoryMb} MB");
        SetLeftField(Pages[4].Page, "主题", State.Theme == "light" ? "浅色" : "深色");
        SetLeftField(Pages[4].Page, "Java", string.IsNullOrEmpty(State.SelectedJavaHome) ? "尚未探测" : "已选择");
        SetLeftField(Pages[5].Page, "主题", State.Theme == "light" ? "浅色" : "深色");
        SetLeftField(Pages[5].Page, "动效", State.ReduceMotion ? "已减弱" : "完整");
    }

    private static void SetLeftField(PageViewModelBase? page, string label, string value)
    {
        if (page?.LeftPanel is LeftPanelViewModel panel)
        {
            panel.Set(label, value);
        }
    }

    /// <summary>选中版本并回到启动页生成计划。</summary>
    public void StartVersion(VersionRowViewModel row)
    {
        State.SelectedVersion = row;
        SelectPage(Pages[0]);
        if (Pages[0].Page is LaunchPageViewModel launch)
        {
            IsFullscreen = false;
            launch.PlanCommand.Execute(null);
        }
    }

    /// <summary>导入整合包（拖入 .mrpack / .zip）。</summary>
    [RelayCommand]
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
            ? $"已导入 {manifest.Name}：覆盖文件 {installed.Value!.OverridesCopied} 个，下载 {installed.Value!.Downloaded.Count} 个，待解析 {installed.Value!.Unresolved.Count} 个。"
            : $"导入失败：{installed.Code}（{installed.Detail}）";
        ShowToast(ImportStatus);

        foreach (NavItem item in Pages)
        {
            if (item.Page is GameListPageViewModel games)
            {
                games.ApplyFilter();
            }

            if (item.Page is DashboardPageViewModel dashboard)
            {
                dashboard.Refresh();
            }
        }
    }

    /// <summary>开始游戏：切到启动页并生成启动计划。</summary>
    [RelayCommand]
    private void StartGame()
    {
        SelectPage(Pages[0]);
        if (Pages[0].Page is LaunchPageViewModel launch)
        {
            launch.PlanCommand.Execute(null);
        }
    }

    /// <summary>关闭前保存设置。</summary>
    public void SaveSettingsOnExit()
    {
        foreach (NavItem item in Pages)
        {
            if (item.Page is SettingsPageViewModel settings)
            {
                settings.SaveSettings();
            }
        }

        ShowToast("设置已保存");
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