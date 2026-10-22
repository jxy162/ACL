using System.Collections.ObjectModel;
using System.Windows.Input;
using ACL.App.Services;
using ACL.Core.Version;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>游戏列表行：本机已安装实例。</summary>
public partial class InstalledInstanceRow : ObservableObject
{
    /// <summary>构造行。</summary>
    public InstalledInstanceRow(InstalledInstance instance, ICommand start, ICommand openSettings)
    {
        Instance = instance;
        StartCommand = start;
        SettingsCommand = openSettings;
        PlayMinutes = 0;
    }

    /// <summary>实例信息。</summary>
    public InstalledInstance Instance { get; }

    /// <summary>实例名。</summary>
    public string Id => Instance.Id;

    /// <summary>加载器标签。</summary>
    public string LoaderLabel => Instance.LoaderLabel;

    /// <summary>安装时间文本。</summary>
    public string InstalledText => Instance.ReleasedTime == default ? "—" : Instance.ReleasedTime.ToLocalTime().ToString("yyyy-MM-dd");

    /// <summary>启动命令。</summary>
    public ICommand StartCommand { get; }

    /// <summary>实例设置命令。</summary>
    public ICommand SettingsCommand { get; }

    /// <summary>累计游玩分钟。</summary>
    [ObservableProperty]
    private double _playMinutes;

    /// <summary>游玩时长文本。</summary>
    public string PlayText => PlayMinutes <= 0
        ? "—"
        : PlayMinutes >= 60 ? $"{PlayMinutes / 60:F1} h" : $"{PlayMinutes:F0} min";

    partial void OnPlayMinutesChanged(double value) => OnPropertyChanged(nameof(PlayText));
}

/// <summary>游戏列表页：只列出本机已安装的游戏，可直接启动或进入实例设置。</summary>
public partial class GameListPageViewModel : PageViewModelBase
{
    private readonly AppServices _services;
    private readonly AppState _state;
    private readonly ILauncherHost _host;

    /// <summary>构造游戏列表页。</summary>
    public GameListPageViewModel(AppServices services, AppState state, ILauncherHost host)
        : base("游戏列表", "本机已安装的游戏，选中即可启动；每个实例都有独立的 Java 与内存设置。")
    {
        _services = services;
        _state = state;
        _host = host;
        StartCommand = new AsyncRelayCommand<InstalledInstanceRow>(StartAsync);
        SettingsCommand = new RelayCommand<InstalledInstanceRow>(row =>
        {
            if (row is not null)
            {
                _host.OpenInstanceSettings(row);
            }
        });

        _state.PlaytimeChanged += (_, _) => Refresh();
    }

    /// <summary>本机实例列表。</summary>
    public ObservableCollection<InstalledInstanceRow> Rows { get; } = [];

    /// <summary>启动命令。</summary>
    public ICommand StartCommand { get; }

    /// <summary>实例设置命令。</summary>
    public ICommand SettingsCommand { get; }

    /// <summary>状态文本。</summary>
    [ObservableProperty]
    private string _status = "尚未发现本机实例。";

    /// <summary>关键字过滤。</summary>
    [ObservableProperty]
    private string _filter = string.Empty;

    partial void OnFilterChanged(string value) => Refresh();

    /// <summary>重新扫描并重建列表。</summary>
    public void Refresh()
    {
        Rows.Clear();
        string keyword = Filter.Trim();
        IReadOnlyList<InstalledInstance> instances = _services.InstanceScanner.Scan(_services.VersionsDirectory);
        _state.Instances.Clear();
        foreach (InstalledInstance instance in instances)
        {
            _state.Instances.Add(instance);
        }

        foreach (InstalledInstance instance in instances)
        {
            if (keyword.Length > 0 && !instance.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Rows.Add(new InstalledInstanceRow(instance, StartCommand, SettingsCommand)
            {
                PlayMinutes = _state.GetPlayMinutes(instance.Id),
            });
        }

        Status = instances.Count == 0
            ? "尚未发现本机实例：请到“下载”页安装一个版本（可附带 Forge / Fabric / OptiFine）。"
            : $"本机共 {instances.Count} 个实例，显示 {Rows.Count} 个。";
    }

    /// <summary>刷新扫描（供主窗口调用）。</summary>
    [RelayCommand]
    private void Rescan() => Refresh();

    private Task StartAsync(InstalledInstanceRow? row)
        => row is null ? Task.CompletedTask : _host.LaunchInstanceAsync(row.Id);

    /// <summary>重写刷新入口（页面重新显示时调用）。</summary>
    public void OnActivated() => Refresh();
}