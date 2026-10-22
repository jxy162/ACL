using System.Collections.ObjectModel;
using ACL.Core.Domain;
using ACL.Core.Launch;
using ACL.Core.Version;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ACL.App.ViewModels;

/// <summary>跨页面共享的状态：账号、版本列表、实例列表与启动设置。</summary>
public partial class AppState : ObservableObject
{
    // ===== 版本与实例 =====

    /// <summary>全部版本（来自官方清单，含正式版 / 快照 / 预览 / 远古）。</summary>
    public ObservableCollection<VersionRowViewModel> Versions { get; } = [];

    /// <summary>本机已安装的实例（扫描 <c>.minecraft/versions</c>）。</summary>
    public ObservableCollection<InstalledInstance> Instances { get; } = [];

    /// <summary>当前选择的版本行。</summary>
    [ObservableProperty]
    private VersionRowViewModel? _selectedVersion;

    /// <summary>当前选择的版本名。</summary>
    [ObservableProperty]
    private string? _selectedVersionId;

    // ===== 账号 =====

    /// <summary>本地已登录账号（离线 / 正版分开记录，仅本机）。</summary>
    public ObservableCollection<AccountProfile> Accounts { get; } = [];

    /// <summary>当前账号档案。</summary>
    [ObservableProperty]
    private AccountProfile? _currentAccount;

    /// <summary>当前账号的认证会话（内存态；离线账号为空会话）。</summary>
    public AuthSession? CurrentSession { get; set; }

    /// <summary>玩家名（离线登录用；与当前账号联动）。</summary>
    [ObservableProperty]
    private string _playerName = "Steve";

    /// <summary>账号状态文本。</summary>
    [ObservableProperty]
    private string _accountStatus = "未登录";

    /// <summary>设备码提示（登录中显示）。</summary>
    [ObservableProperty]
    private string _deviceCodeText = string.Empty;

    /// <summary>是否登录中。</summary>
    [ObservableProperty]
    private bool _isAuthenticating;

    /// <summary>账号列表变化事件。</summary>
    public event EventHandler? AccountsChanged;

    /// <summary>列出某类型的本地账号。</summary>
    public IReadOnlyList<AccountProfile> AccountsOfType(AccountType type)
        => [.. Accounts.Where(account => account.Type == type)];

    /// <summary>切换到指定账号。</summary>
    public void ActivateAccount(AccountProfile account, AuthSession? session)
    {
        CurrentAccount = account;
        CurrentSession = session ?? (account.Type == AccountType.Offline
            ? OfflineIdentity.CreateSession(account.Name)
            : null);
        PlayerName = account.Name;
        AccountStatus = $"{account.TypeLabel} · {account.Name}";
    }

    /// <summary>通知账号列表已变化。</summary>
    public void RaiseAccountsChanged() => AccountsChanged?.Invoke(this, EventArgs.Empty);

    // ===== 启动动画（全屏覆盖层） =====

    /// <summary>是否正在启动（驱动启动动画覆盖层）。</summary>
    [ObservableProperty]
    private bool _isLaunching;

    /// <summary>启动动画标题。</summary>
    [ObservableProperty]
    private string _launchTitle = "正在启动游戏";

    /// <summary>启动动画中的实例名。</summary>
    [ObservableProperty]
    private string _launchVersionId = string.Empty;

    /// <summary>当前阶段。</summary>
    [ObservableProperty]
    private string _launchStage = "初始化";

    /// <summary>启动进度（0-1）。</summary>
    [ObservableProperty]
    private double _launchProgress;

    /// <summary>登录方式文本。</summary>
    [ObservableProperty]
    private string _launchMethod = "离线登录";

    /// <summary>下载速度文本。</summary>
    [ObservableProperty]
    private string _launchSpeed = string.Empty;

    /// <summary>启动小提示。</summary>
    [ObservableProperty]
    private string _launchHint = "正在准备运行环境…";

    /// <summary>是否可以取消启动。</summary>
    [ObservableProperty]
    private bool _canCancelLaunch;

    /// <summary>启动动画开始时间（用于计算已用时间）。</summary>
    public DateTimeOffset LaunchStartedAt { get; set; } = DateTimeOffset.UtcNow;

    // ===== 启动设置 =====

    /// <summary>最小堆（MB）。</summary>
    [ObservableProperty]
    private double _minMemoryMb = 512;

    /// <summary>最大堆（MB）。</summary>
    [ObservableProperty]
    private double _maxMemoryMb = 4096;

    /// <summary>是否优先官方源。</summary>
    [ObservableProperty]
    private bool _preferOfficial;

    /// <summary>当前选择的 Java 主目录。</summary>
    [ObservableProperty]
    private string? _selectedJavaHome;

    /// <summary>各实例的独立设置。</summary>
    public Dictionary<string, InstanceSettings> InstanceSettings { get; } = new(StringComparer.Ordinal);

    /// <summary>读取某实例的设置（不存在时返回空设置）。</summary>
    public InstanceSettings GetInstanceSettings(string id)
        => InstanceSettings.TryGetValue(id, out InstanceSettings? settings) ? settings : new InstanceSettings();

    /// <summary>写入某实例的设置。</summary>
    public void SetInstanceSettings(string id, InstanceSettings settings)
    {
        InstanceSettings[id] = settings;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>实例设置变化事件（用于持久化）。</summary>
    public event EventHandler? SettingsChanged;

    // ===== 游玩时长 =====

    /// <summary>游玩时长变化事件。</summary>
    public event EventHandler? PlaytimeChanged;

    /// <summary>各版本累计游玩分钟数。</summary>
    public Dictionary<string, double> PlayMinutes { get; } = new(StringComparer.Ordinal);

    /// <summary>累加某版本的游玩时长。</summary>
    public void AddPlayMinutes(string versionId, double minutes)
    {
        if (minutes <= 0)
        {
            return;
        }

        PlayMinutes[versionId] = PlayMinutes.TryGetValue(versionId, out double current) ? current + minutes : minutes;
        PlaytimeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>读取某版本累计时长。</summary>
    public double GetPlayMinutes(string versionId)
        => PlayMinutes.TryGetValue(versionId, out double minutes) ? minutes : 0;

    // ===== 轻提示与退出信息 =====

    /// <summary>轻提示请求事件。</summary>
    public event EventHandler<string>? ToastRequested;

    /// <summary>请求显示轻提示。</summary>
    public void RequestToast(string text) => ToastRequested?.Invoke(this, text);

    /// <summary>最近一次运行结果变化事件。</summary>
    public event EventHandler? LastExitChanged;

    /// <summary>记录最近一次运行结果。</summary>
    public void SetLastExit(GameExitInfo exit)
    {
        LastExit = exit;
        LastExitChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>最近一次游戏退出的信息。</summary>
    [ObservableProperty]
    private GameExitInfo? _lastExit;

    /// <summary>最近一次运行的日志文件路径。</summary>
    [ObservableProperty]
    private string _lastLogPath = string.Empty;

    // ===== 外观 =====

    /// <summary>是否减弱动效。</summary>
    [ObservableProperty]
    private bool _reduceMotion;

    /// <summary>主题（dark / light）。</summary>
    [ObservableProperty]
    private string _theme = "dark";

    /// <summary>头像占位字符。</summary>
    public string AvatarLetter => string.IsNullOrWhiteSpace(PlayerName) ? "A" : PlayerName[..1].ToUpperInvariant();

    /// <summary>Hero 大号标题。</summary>
    public string VersionTitle => string.IsNullOrWhiteSpace(SelectedVersionId) ? "未选择版本" : SelectedVersionId!;

    /// <summary>版本徽标文本。</summary>
    public string VersionBadge => SelectedVersion is { } row
        ? $"{row.CategoryLabel} · {row.Released}"
        : "版本：未选择";

    /// <summary>账号徽标文本。</summary>
    public string AccountBadge => CurrentAccount is { } account
        ? $"账号：{account.Name}（{account.TypeLabel}）"
        : "账号：未登录";

    /// <summary>当前版本是否已安装。</summary>
    public bool CurrentVersionInstalled => string.IsNullOrWhiteSpace(SelectedVersionId)
        || Instances.Any(instance => string.Equals(instance.Id, SelectedVersionId, StringComparison.Ordinal));

    partial void OnSelectedVersionChanged(VersionRowViewModel? value)
    {
        if (value is not null)
        {
            SelectedVersionId = value.Id;
        }
    }

    partial void OnSelectedVersionIdChanged(string? value)
    {
        OnPropertyChanged(nameof(VersionTitle));
        OnPropertyChanged(nameof(VersionBadge));
        OnPropertyChanged(nameof(CurrentVersionInstalled));
    }

    partial void OnPlayerNameChanged(string value)
    {
        OnPropertyChanged(nameof(AvatarLetter));
    }

    partial void OnCurrentAccountChanged(AccountProfile? value)
    {
        OnPropertyChanged(nameof(AccountBadge));
    }

    partial void OnLaunchProgressChanged(double value) => OnPropertyChanged(nameof(LaunchProgressText));

    /// <summary>启动进度文本。</summary>
    public string LaunchProgressText => $"{LaunchProgress * 100:F2} %";
}