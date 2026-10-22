using System.Diagnostics;
using ACL.App.Services;
using ACL.Core.Domain;
using ACL.Core.Launch;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>仪表板：运行统计、最近一次启动结果与诊断入口。</summary>
public partial class DashboardPageViewModel : PageViewModelBase
{

    private readonly AppServices _services;
    private readonly AppState _state;

    /// <summary>构造仪表板。</summary>
    public DashboardPageViewModel(AppServices services, AppState state)
        : base("仪表板", "运行统计与最近一次启动结果，便于快速定位问题。")
    {
        _services = services;
        _state = state;
        _state.LastExitChanged += (_, _) => Refresh();
        _state.PlaytimeChanged += (_, _) => Refresh();
    }

    /// <summary>累计游玩时长（分钟）。</summary>
    [ObservableProperty]
    private double _totalMinutes;

    /// <summary>版本总数。</summary>
    [ObservableProperty]
    private int _versionCount;

    /// <summary>已安装数量。</summary>
    [ObservableProperty]
    private int _installedCount;

    /// <summary>最近一次结果文本。</summary>
    [ObservableProperty]
    private string _lastExitText = "本次会话尚未启动过游戏。";

    /// <summary>最近一次崩溃归因文本。</summary>
    [ObservableProperty]
    private string _lastReasonText = "—";

    /// <summary>最近一次日志尾部。</summary>
    [ObservableProperty]
    private string _lastLogTail = string.Empty;

    /// <summary>环境信息。</summary>
    public string EnvironmentText => $"内存 {_state.MinMemoryMb:F0}-{_state.MaxMemoryMb:F0} MB · 主题 {_state.Theme} · Java {(_state.SelectedJavaHome ?? "尚未探测")}";

    /// <summary>日志目录。</summary>
    public string LogDirectory => _services.LogDirectory;

    /// <summary>刷新统计。</summary>
    [RelayCommand]
    public void Refresh()
    {
        TotalMinutes = _state.PlayMinutes.Values.Sum();
        VersionCount = _state.Versions.Count;

        int installed = 0;
        foreach (VersionRowViewModel row in _state.Versions)
        {
            if (_services.FileSystem.FileExists(Path.Combine(_services.GameDirectory, "versions", row.Id, row.Id + ".jar")))
            {
                installed++;
            }
        }

        InstalledCount = installed;
        OnPropertyChanged(nameof(EnvironmentText));

        GameExitInfo? exit = _state.LastExit;
        if (exit is null)
        {
            LastExitText = "本次会话尚未启动过游戏。";
            LastReasonText = "—";
            LastLogTail = string.Empty;
        }
        else
        {
            LastExitText = $"状态 {exit.State} · 退出码 {exit.ExitCode} · 时长 {exit.Duration.TotalMinutes:F1} 分钟";
            LastReasonText = Describe(exit.Reason);
            LastLogTail = exit.LogTail ?? string.Empty;
        }
    }

    /// <summary>打开日志目录。</summary>
    [RelayCommand]
    private void OpenLogDirectory()
    {
        try
        {
            _services.FileSystem.CreateDirectory(_services.LogDirectory);
            Process.Start(new ProcessStartInfo { FileName = _services.LogDirectory, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // 忽略打开失败。
        }
    }

    private static string Describe(GameExitReason reason) => reason switch
    {
        GameExitReason.NormalExit => "正常退出",
        GameExitReason.JavaError => "Java 错误（类版本/启动异常）",
        GameExitReason.MemoryError => "内存不足（OutOfMemoryError）",
        GameExitReason.GraphicsDriverError => "显卡 / 驱动相关（访问违例）",
        GameExitReason.NativeCrash => "原生层崩溃",
        GameExitReason.ModError => "模组相关错误",
        _ => "未知原因",
    };
}