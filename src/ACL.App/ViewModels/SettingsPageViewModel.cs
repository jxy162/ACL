using System.Collections.ObjectModel;
using ACL.App.Services;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>设置页。</summary>
public partial class SettingsPageViewModel : PageViewModelBase
{
    private readonly AppServices _services;

    /// <summary>构建设置页。</summary>
    public SettingsPageViewModel(AppServices services, AppState state)
        : base("设置", "内存、镜像源与 Java 运行时（配置持久化在 M9 落地）。")
    {
        _services = services;
        State = state;
    }

    /// <summary>共享设置。</summary>
    public AppState State { get; }

    /// <summary>探测到的 Java。</summary>
    public ObservableCollection<string> JavaRuntimes { get; } = [];

    /// <summary>状态。</summary>
    [ObservableProperty]
    private string _status = "点击“探测 Java”以扫描本机运行时。";

    /// <summary>设置文件路径。</summary>
    public string SettingsPath => Path.Combine(_services.DataDirectory, "ACL.config.json");

    /// <summary>保存状态。</summary>
    [ObservableProperty]
    private string _settingsStatus = string.Empty;

    /// <summary>保存设置到便携文件。</summary>
    [RelayCommand]
    public void SaveSettings()
    {
        AclSettings settings = new()
        {
            MinMemoryMb = (int)State.MinMemoryMb,
            MaxMemoryMb = (int)State.MaxMemoryMb,
            PreferOfficialMirror = State.PreferOfficial,
            PlayerName = State.PlayerName,
            SelectedVersionId = State.SelectedVersionId,
        };

        Result saved = _services.Settings.Save(settings);
        SettingsStatus = saved.IsSuccess ? $"已保存到 {SettingsPath}" : $"保存失败：{saved.Code}";
    }

    /// <summary>数据目录。</summary>
    public string DataDirectory => _services.DataDirectory;

    /// <summary>游戏目录。</summary>
    public string GameDirectory => _services.GameDirectory;

    /// <summary>探测 Java。</summary>
    [RelayCommand]
    private async Task DetectJavaAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Status = "正在探测 Java…";
        JavaRuntimes.Clear();
        try
        {
            IReadOnlyList<JavaRuntime> runtimes = await _services.JavaLocator.LocateAsync().ConfigureAwait(true);
            foreach (JavaRuntime runtime in runtimes)
            {
                JavaRuntimes.Add($"{runtime.Version} {(runtime.Is64Bit ? "x64" : "x86")}  {runtime.HomeFolder}");
            }

            Status = runtimes.Count == 0 ? "未发现可用的 Java。" : $"发现 {runtimes.Count} 个 Java 运行时。";
            State.SelectedJavaHome = runtimes.Count > 0 ? runtimes[0].HomeFolder : null;
        }
        finally
        {
            IsBusy = false;
        }
    }
}