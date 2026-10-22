using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ACL.App.ViewModels;

/// <summary>跨页面共享的状态：账号、版本列表与启动设置。</summary>
public partial class AppState : ObservableObject
{
    /// <summary>版本列表（侧栏与下载页共享）。</summary>
    public ObservableCollection<VersionRowViewModel> Versions { get; } = [];

    /// <summary>当前选择的版本行。</summary>
    [ObservableProperty]
    private VersionRowViewModel? _selectedVersion;

    /// <summary>当前选择的版本名。</summary>
    [ObservableProperty]
    private string? _selectedVersionId;

    /// <summary>玩家名（离线登录）。</summary>
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

    /// <summary>最小堆（MB）。</summary>
    [ObservableProperty]
    private double _minMemoryMb = 512;

    /// <summary>最大堆（MB）。</summary>
    [ObservableProperty]
    private double _maxMemoryMb = 4096;

    /// <summary>是否优先官方源。</summary>
    [ObservableProperty]
    private bool _preferOfficial = true;

    /// <summary>游玩时长变化事件（用于持久化）。</summary>
    public event EventHandler? PlaytimeChanged;

    /// <summary>各版本累计游玩分钟数。</summary>
    public Dictionary<string, double> PlayMinutes { get; } = new(StringComparer.Ordinal);

    /// <summary>累加某版本的游玩时长并触发持久化。</summary>
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

    /// <summary>主题（dark / light）。</summary>
    [ObservableProperty]
    private string _theme = "dark";

    /// <summary>当前选择的 Java 主目录。</summary>
    [ObservableProperty]
    private string? _selectedJavaHome;

    /// <summary>头像占位字符。</summary>
    public string AvatarLetter => string.IsNullOrWhiteSpace(PlayerName) ? "A" : PlayerName[..1].ToUpperInvariant();

    /// <summary>Hero 大号标题。</summary>
    public string VersionTitle => string.IsNullOrWhiteSpace(SelectedVersionId) ? "未选择版本" : SelectedVersionId!;

    /// <summary>版本徽标文本。</summary>
    public string VersionBadge => $"版本：{(string.IsNullOrWhiteSpace(SelectedVersionId) ? "未选择" : SelectedVersionId)}";

    /// <summary>账号徽标文本。</summary>
    public string AccountBadge => $"账号：{PlayerName}（离线）";

    // 下拉框在列表重建时会瞬时写回 null，这里只在有效选择时更新“以此为准”的版本名。
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
    }

    partial void OnPlayerNameChanged(string value)
    {
        OnPropertyChanged(nameof(AvatarLetter));
        OnPropertyChanged(nameof(AccountBadge));
    }
}