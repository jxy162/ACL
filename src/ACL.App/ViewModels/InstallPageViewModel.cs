using System.Collections.ObjectModel;
using System.Windows.Input;
using ACL.App.Services;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Resources;
using ACL.Core.Version.Loaders;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>
/// 安装二级页面：安装所选 Minecraft 版本，并可进行 Forge / NeoForge / Fabric /
/// Quilt / OptiFine / Iris 等前置安装。
/// </summary>
public partial class InstallPageViewModel : PageViewModelBase
{
    private readonly AppServices _services;
    private readonly AppState _state;
    private readonly ILauncherHost _host;
    private bool _nameEdited;

    /// <summary>构造安装页。</summary>
    public InstallPageViewModel(AppServices services, AppState state, ILauncherHost host, VersionRowViewModel version)
        : base($"安装 {version.Id}", "先安装基础版本，再按需叠加加载器；也可以只装原版。")
    {
        _services = services;
        _state = state;
        _host = host;
        Version = version;
        _instanceName = version.Id;

        InstallCommand = new AsyncRelayCommand(InstallAsync, () => !IsBusy);
        BackCommand = new RelayCommand(() => _host.ShowPage("download"));
        DetectJavaCommand = new AsyncRelayCommand(DetectJavaAsync);
        LaunchCommand = new AsyncRelayCommand(LaunchAsync, () => InstalledInstanceId is not null);
    }

    /// <summary>目标版本。</summary>
    public VersionRowViewModel Version { get; }

    /// <summary>可选加载器。</summary>
    public ObservableCollection<string> LoaderKinds { get; } =
        ["原版", "Forge", "NeoForge", "Fabric", "Quilt", "OptiFine", "Iris"];

    /// <summary>当前加载器。</summary>
    [ObservableProperty]
    private string _loaderKind = "原版";

    /// <summary>可选的加载器版本。</summary>
    public ObservableCollection<LoaderOffer> LoaderVersions { get; } = [];

    /// <summary>当前加载器版本。</summary>
    [ObservableProperty]
    private LoaderOffer? _selectedLoaderVersion;

    /// <summary>目标实例名。</summary>
    [ObservableProperty]
    private string _instanceName;

    /// <summary>检测到的 Java。</summary>
    public ObservableCollection<string> JavaCandidates { get; } = [];

    /// <summary>当前 Java 主目录。</summary>
    [ObservableProperty]
    private string? _selectedJavaHome;

    /// <summary>状态文本。</summary>
    [ObservableProperty]
    private string _status = "选择加载器后点击“开始安装”。";

    /// <summary>日志。</summary>
    public ObservableCollection<string> Log { get; } = [];

    /// <summary>进度。</summary>
    [ObservableProperty]
    private double _progress;

    /// <summary>已安装的实例名（成功后可启动）。</summary>
    [ObservableProperty]
    private string? _installedInstanceId;

    /// <summary>加载器版本加载中。</summary>
    [ObservableProperty]
    private bool _isLoadingLoaders;

    /// <summary>是否需要加载器版本选择。</summary>
    public bool NeedsLoaderVersion => LoaderKind is not ("原版" or "Iris");

    /// <summary>说明文本。</summary>
    public string KindHint => LoaderKind switch
    {
        "原版" => "仅安装官方基础版本，可直接启动。",
        "Forge" => "下载官方安装器并以 --installClient 无交互执行，需要本机 Java。",
        "NeoForge" => "同 Forge，使用 NeoForge 官方安装器（1.20.1 走 legacy 分支）。",
        "Fabric" => "直接使用 Fabric 官方 install profile，无需 Java。",
        "Quilt" => "使用 Quilt 官方 install profile，无需 Java。",
        "OptiFine" => "下载 OptiFine 补丁包并按 Maven 布局落盘，生成继承实例（建议叠加在 Forge 上）。",
        "Iris" => "先安装 Fabric，再从 Modrinth 拉取 Iris 光影前置模组到 mods 目录。",
        _ => string.Empty,
    };

    /// <summary>安装命令。</summary>
    public IAsyncRelayCommand InstallCommand { get; }

    /// <summary>返回下载页。</summary>
    public ICommand BackCommand { get; }

    /// <summary>探测 Java。</summary>
    public ICommand DetectJavaCommand { get; }

    /// <summary>启动刚安装的实例。</summary>
    public IAsyncRelayCommand LaunchCommand { get; }

    /// <summary>是否需要 Java 才能安装。</summary>
    public bool NeedsJava => LoaderKind is "Forge" or "NeoForge";

    partial void OnLoaderKindChanged(string value)
    {
        OnPropertyChanged(nameof(NeedsLoaderVersion));
        OnPropertyChanged(nameof(KindHint));
        OnPropertyChanged(nameof(NeedsJava));
        UpdateDefaultName();
        _ = LoadLoaderVersionsAsync();
    }

    partial void OnSelectedLoaderVersionChanged(LoaderOffer? value) => UpdateDefaultName();

    partial void OnInstanceNameChanged(string value)
    {
        if (!value.StartsWith(Version.Id, StringComparison.OrdinalIgnoreCase))
        {
            _nameEdited = true;
        }
    }

    private void UpdateDefaultName()
    {
        if (_nameEdited)
        {
            return;
        }

        InstanceName = LoaderKind switch
        {
            "原版" => Version.Id,
            "OptiFine" => $"{Version.Id}-OptiFine",
            "Iris" => $"{Version.Id}-fabric-iris",
            _ => SelectedLoaderVersion is { } offer ? $"{Version.Id}-{offer.Kind.ToString().ToLowerInvariant()}-{offer.Version}" : Version.Id,
        };
    }

    private async Task LoadLoaderVersionsAsync()
    {
        LoaderVersions.Clear();
        SelectedLoaderVersion = null;
        if (!NeedsLoaderVersion)
        {
            return;
        }

        ModLoaderKind kind = CurrentKind();
        if (kind == ModLoaderKind.None)
        {
            return;
        }

        IsLoadingLoaders = true;
        Status = $"正在获取 {LoaderKind} 版本列表…";
        try
        {
            Result<IReadOnlyList<LoaderOffer>> result = await _services.LoaderCatalog
                .GetVersionsAsync(kind, Version.Id)
                .ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                Status = $"获取 {LoaderKind} 版本失败：{result.Code}";
                return;
            }

            foreach (LoaderOffer offer in result.Value!)
            {
                LoaderVersions.Add(offer);
            }

            SelectedLoaderVersion = LoaderVersions.FirstOrDefault();
            Status = LoaderVersions.Count == 0
                ? $"{Version.Id} 没有可用的 {LoaderKind} 版本。"
                : $"{LoaderKind}：共 {LoaderVersions.Count} 个版本。";
        }
        catch (Exception ex)
        {
            Status = $"获取加载器版本失败：{ex.GetType().Name}";
        }
        finally
        {
            IsLoadingLoaders = false;
            UpdateDefaultName();
        }
    }

    private async Task InstallAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        InstallCommand.NotifyCanExecuteChanged();
        Log.Clear();
        Progress = 0;
        InstalledInstanceId = null;
        try
        {
            ModLoaderKind kind = CurrentKind();
            if (kind != ModLoaderKind.None && SelectedLoaderVersion is null)
            {
                Status = "请先选择加载器版本。";
                return;
            }

            if (NeedsJava && string.IsNullOrWhiteSpace(SelectedJavaHome))
            {
                Status = "Forge / NeoForge 需要 Java，请先点击“探测 Java”。";
                return;
            }

            if (kind == ModLoaderKind.None)
            {
                await InstallVanillaAsync().ConfigureAwait(true);
                return;
            }

            if (LoaderKind == "Iris")
            {
                await InstallIrisAsync().ConfigureAwait(true);
                return;
            }

            Status = $"正在安装 {InstanceName}…";
            UiProgressSink sink = new(report =>
            {
                Progress = report.Fraction;
                Status = $"{report.Stage} · {InstanceName}";
                if (report.Message is { Length: > 0 } message && Log.Count < 400)
                {
                    Log.Add($"[{report.Stage}] {message}");
                }
            });

            LoaderInstallRequest request = new(
                kind,
                Version.Id,
                SelectedLoaderVersion!.Version,
                InstanceName,
                _services.GameDirectory,
                JavaExecutable(),
                LoaderKind == "OptiFine" ? null : null,
                SelectedLoaderVersion.DownloadUri);

            Result<LoaderInstallResult> result = await _services.LoaderInstaller
                .InstallAsync(request, sink)
                .ConfigureAwait(true);

            ApplyResult(result);
        }
        catch (Exception ex)
        {
            Status = $"安装失败：{ex.GetType().Name}";
            Log.Add($"[error] {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            InstallCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task InstallVanillaAsync()
    {
        Status = $"正在安装原版 {Version.Id}…";
        UiProgressSink sink = new(report =>
        {
            Progress = report.Fraction;
            Status = $"{report.Stage} · {Version.Id}";
        });

        Result<LoaderInstallResult> result = await _services.LoaderInstaller
            .InstallAsync(new LoaderInstallRequest(ModLoaderKind.None, Version.Id, null, Version.Id, _services.GameDirectory), sink)
            .ConfigureAwait(true);
        ApplyResult(result);
    }

    private async Task InstallIrisAsync()
    {
        ModLoaderKind fabric = ModLoaderKind.Fabric;
        Result<IReadOnlyList<LoaderOffer>> loaders = await _services.LoaderCatalog
            .GetVersionsAsync(fabric, Version.Id)
            .ConfigureAwait(true);
        if (!loaders.IsSuccess || loaders.Value!.Count == 0)
        {
            Status = "未能获取 Fabric 版本（Iris 依赖 Fabric）。";
            return;
        }

        LoaderOffer fabricLoader = SelectedLoaderVersion ?? loaders.Value![0];
        string fabricName = $"{Version.Id}-fabric-{fabricLoader.Version}";
        Status = $"正在安装 Fabric {fabricLoader.Version}…";
        UiProgressSink sink = new(report =>
        {
            Progress = report.Fraction * 0.8;
            Status = $"{report.Stage} · {fabricName}";
        });

        Result<LoaderInstallResult> fabricResult = await _services.LoaderInstaller.InstallAsync(
            new LoaderInstallRequest(fabric, Version.Id, fabricLoader.Version, fabricName, _services.GameDirectory),
            sink).ConfigureAwait(true);
        if (!fabricResult.IsSuccess)
        {
            ApplyResult(fabricResult);
            return;
        }

        Status = "正在下载 Iris 前置模组…";
        Result<IReadOnlyList<ResourceFile>> files = await _services.Modrinth
            .GetFilesAsync("iris", Version.Id, fabric)
            .ConfigureAwait(true);
        if (!files.IsSuccess || files.Value!.Count == 0)
        {
            Status = "Fabric 已安装，但未能获取 Iris 模组（可在“资源”页手动下载）。";
            InstalledInstanceId = fabricResult.Value!.InstanceId;
            return;
        }

        ResourceFile file = files.Value[0];
        string target = Path.Combine(_services.GameDirectory, "mods", file.FileName);
        Result<Core.Download.DownloadOutcome> downloaded = await _services.Download
            .DownloadAsync(new Core.Domain.DownloadItem(file.Url, [], target, file.PreferredHash, file.Size), sink)
            .ConfigureAwait(true);

        InstalledInstanceId = fabricResult.Value!.InstanceId;
        Progress = 1;
        Status = downloaded.IsSuccess
            ? $"Iris 前置安装完成：Fabric {fabricLoader.Version} + {file.FileName}（建议再安装 Sodium）。"
            : $"Fabric 已安装，Iris 下载失败：{downloaded.Code}";
        _host.RefreshInstances();
        _host.ShowToast($"已安装 {InstalledInstanceId}");
        LaunchCommand.NotifyCanExecuteChanged();
    }

    private void ApplyResult(Result<LoaderInstallResult> result)
    {
        if (!result.IsSuccess)
        {
            Status = $"安装失败：{result.Code}（{result.Detail}）";
            return;
        }

        LoaderInstallResult value = result.Value!;
        InstalledInstanceId = value.InstanceId;
        Progress = 1;
        Status = $"安装完成：{value.InstanceId}";
        foreach (string note in value.Notes)
        {
            Log.Add(note);
        }

        _host.RefreshInstances();
        _host.ShowToast($"安装完成：{value.InstanceId}");
        LaunchCommand.NotifyCanExecuteChanged();
    }

    private async Task LaunchAsync()
    {
        if (InstalledInstanceId is { Length: > 0 } id)
        {
            await _host.LaunchInstanceAsync(id).ConfigureAwait(true);
        }
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
                JavaCandidates.Add($"{runtime.Version} · {runtime.HomeFolder}");
            }

            SelectedJavaHome = runtimes.FirstOrDefault()?.HomeFolder;
            Status = runtimes.Count == 0 ? "未发现 Java，请先安装 JDK/JRE。" : $"发现 {runtimes.Count} 个 Java 运行时。";
        }
        catch (Exception ex)
        {
            Status = $"探测失败：{ex.GetType().Name}";
        }
    }

    private string? JavaExecutable()
    {
        if (string.IsNullOrWhiteSpace(SelectedJavaHome))
        {
            return null;
        }

        string exe = OperatingSystem.IsWindows() ? "java.exe" : "java";
        return Path.Combine(SelectedJavaHome!, "bin", exe);
    }

    private ModLoaderKind CurrentKind() => LoaderKind switch
    {
        "Forge" => ModLoaderKind.Forge,
        "NeoForge" => ModLoaderKind.NeoForge,
        "Fabric" or "Iris" => ModLoaderKind.Fabric,
        "Quilt" => ModLoaderKind.Quilt,
        "OptiFine" => ModLoaderKind.OptiFine,
        _ => ModLoaderKind.None,
    };
}