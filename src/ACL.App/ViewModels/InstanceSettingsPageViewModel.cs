using System.Collections.ObjectModel;
using System.Windows.Input;
using ACL.App.Services;
using ACL.Core.Domain;
using ACL.Core.Version;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>实例设置二级页面：每个实例独立的 Java / 内存 / 参数设置。</summary>
public partial class InstanceSettingsPageViewModel : PageViewModelBase
{
    private readonly AppServices _services;
    private readonly AppState _state;
    private readonly ILauncherHost _host;

    /// <summary>构造实例设置页。</summary>
    public InstanceSettingsPageViewModel(AppServices services, AppState state, ILauncherHost host, InstalledInstance instance)
        : base($"实例设置 · {instance.Id}", "仅作用于该实例；留空表示沿用全局设置。")
    {
        _services = services;
        _state = state;
        _host = host;
        Instance = instance;

        InstanceSettings current = state.GetInstanceSettings(instance.Id);
        _maxMemory = current.MaxMemoryMb ?? (int)state.MaxMemoryMb;
        _minMemory = current.MinMemoryMb ?? (int)state.MinMemoryMb;
        _javaPath = current.JavaPath ?? string.Empty;
        _extraJvmArgs = current.ExtraJvmArgs ?? string.Empty;
        _extraGameArgs = current.ExtraGameArgs ?? string.Empty;
        _workingDir = current.WorkingDir ?? string.Empty;
        _note = current.Note ?? string.Empty;

        // 默认 Java 目录显示（未显式设置时）。
        _effectiveJava = string.IsNullOrWhiteSpace(_javaPath)
            ? state.SelectedJavaHome ?? "自动选择（按版本需求挑选）"
            : _javaPath;

        SaveCommand = new RelayCommand(Save);
        BackCommand = new RelayCommand(() => _host.ShowPage("games"));
        LaunchCommand = new AsyncRelayCommand(() => _host.LaunchInstanceAsync(Instance.Id));
        DetectJavaCommand = new AsyncRelayCommand(DetectJavaAsync);
        ClearJavaCommand = new RelayCommand(() => JavaPath = string.Empty);
    }

    /// <summary>目标实例。</summary>
    public InstalledInstance Instance { get; }

    /// <summary>最大堆（MB）。</summary>
    [ObservableProperty]
    private double _maxMemory;

    /// <summary>最小堆（MB）。</summary>
    [ObservableProperty]
    private double _minMemory;

    /// <summary>Java 可执行文件路径（留空自动选择）。</summary>
    [ObservableProperty]
    private string _javaPath;

    /// <summary>附加 JVM 参数。</summary>
    [ObservableProperty]
    private string _extraJvmArgs;

    /// <summary>附加游戏参数。</summary>
    [ObservableProperty]
    private string _extraGameArgs;

    /// <summary>自定义工作目录。</summary>
    [ObservableProperty]
    private string _workingDir;

    /// <summary>备注。</summary>
    [ObservableProperty]
    private string _note;

    /// <summary>生效的 Java 描述。</summary>
    [ObservableProperty]
    private string _effectiveJava = string.Empty;

    /// <summary>探测到的 Java 列表。</summary>
    public ObservableCollection<string> JavaCandidates { get; } = [];

    /// <summary>状态文本。</summary>
    [ObservableProperty]
    private string _status = "修改后点击“保存设置”。";

    /// <summary>实例详情文本。</summary>
    public string Details => $"加载器：{Instance.LoaderLabel} · 客户端 jar：{(Instance.HasClientJar ? "有" : "无")} · 父版本：{Instance.InheritsFrom ?? "—"}";

    /// <summary>保存命令。</summary>
    public ICommand SaveCommand { get; }

    /// <summary>返回游戏列表。</summary>
    public ICommand BackCommand { get; }

    /// <summary>启动命令。</summary>
    public ICommand LaunchCommand { get; }

    /// <summary>探测 Java 命令。</summary>
    public ICommand DetectJavaCommand { get; }

    /// <summary>清除 Java 指定。</summary>
    public ICommand ClearJavaCommand { get; }

    private void Save()
    {
        InstanceSettings settings = new()
        {
            MaxMemoryMb = (int)Math.Round(MaxMemory),
            MinMemoryMb = (int)Math.Round(MinMemory),
            JavaPath = string.IsNullOrWhiteSpace(JavaPath) ? null : JavaPath.Trim(),
            ExtraJvmArgs = string.IsNullOrWhiteSpace(ExtraJvmArgs) ? null : ExtraJvmArgs.Trim(),
            ExtraGameArgs = string.IsNullOrWhiteSpace(ExtraGameArgs) ? null : ExtraGameArgs.Trim(),
            WorkingDir = string.IsNullOrWhiteSpace(WorkingDir) ? null : WorkingDir.Trim(),
            Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
        };

        _state.SetInstanceSettings(Instance.Id, settings);
        EffectiveJava = settings.JavaPath ?? _state.SelectedJavaHome ?? "自动选择（按版本需求挑选）";
        Status = $"已保存：最大堆 {settings.MaxMemoryMb} MB、最小堆 {settings.MinMemoryMb} MB。";
        _host.ShowToast($"已保存 {Instance.Id} 的实例设置");
    }

    private async Task DetectJavaAsync()
    {
        Status = "正在探测本机 Java…";
        JavaCandidates.Clear();
        try
        {
            IReadOnlyList<JavaRuntime> runtimes = await _services.JavaLocator.LocateAsync().ConfigureAwait(true);
            foreach (JavaRuntime runtime in runtimes)
            {
                string exe = OperatingSystem.IsWindows() ? "java.exe" : "java";
                JavaCandidates.Add(Path.Combine(runtime.HomeFolder, "bin", exe));
            }

            Status = runtimes.Count == 0 ? "未发现 Java。" : $"发现 {runtimes.Count} 个 Java 运行时，可直接填入上方路径。";
        }
        catch (Exception ex)
        {
            Status = $"探测失败：{ex.GetType().Name}";
        }
    }
}