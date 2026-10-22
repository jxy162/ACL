using CommunityToolkit.Mvvm.ComponentModel;

namespace ACL.App.ViewModels;

/// <summary>跨页面共享的设置与选择项。</summary>
public partial class AppState : ObservableObject
{
    /// <summary>最小堆（MB）。</summary>
    [ObservableProperty]
    private double _minMemoryMb = 512;

    /// <summary>最大堆（MB）。</summary>
    [ObservableProperty]
    private double _maxMemoryMb = 2048;

    /// <summary>是否优先官方源。</summary>
    [ObservableProperty]
    private bool _preferOfficial = true;

    /// <summary>当前选择登录的玩家名（离线）。</summary>
    [ObservableProperty]
    private string _playerName = "Steve";

    /// <summary>当前选择的版本。</summary>
    [ObservableProperty]
    private string? _selectedVersionId;

    /// <summary>当前选择的 Java 主目录。</summary>
    [ObservableProperty]
    private string? _selectedJavaHome;
}