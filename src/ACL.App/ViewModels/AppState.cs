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

    /// <summary>当前选择的 Java 主目录。</summary>
    [ObservableProperty]
    private string? _selectedJavaHome;

    /// <summary>头像占位字符。</summary>
    public string AvatarLetter => string.IsNullOrWhiteSpace(PlayerName) ? "A" : PlayerName[..1].ToUpperInvariant();

    partial void OnSelectedVersionChanged(VersionRowViewModel? value) => SelectedVersionId = value?.Id;

    partial void OnPlayerNameChanged(string value) => OnPropertyChanged(nameof(AvatarLetter));
}