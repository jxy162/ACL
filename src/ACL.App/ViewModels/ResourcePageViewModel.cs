using System.Collections.ObjectModel;
using System.Windows.Input;
using ACL.App.Services;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Modpack;
using ACL.Core.Resources;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>
/// 资源页：推荐资源 + 检索 + 一键安装 Mod / 资源包 / 光影 / 数据包，
/// 并支持直接下载整合包并进入游戏列表。
/// </summary>
public partial class ResourcePageViewModel : PageViewModelBase
{
    private readonly AppServices _services;
    private readonly AppState _state;
    private readonly ILauncherHost _host;
    private readonly List<ResourceRowViewModel> _recommended = [];

    /// <summary>构造资源页。</summary>
    public ResourcePageViewModel(AppServices services, AppState state, ILauncherHost host)
        : base("资源", "推荐资源与检索：Mod / 资源包 / 光影 / 数据包可直接安装；整合包可一键下载并进入游戏列表。")
    {
        _services = services;
        _state = state;
        _host = host;
        _gameVersion = state.SelectedVersionId ?? string.Empty;

        InstallCommand = new AsyncRelayCommand<ResourceRowViewModel>(InstallAsync);
        InstallPackCommand = new AsyncRelayCommand<ResourceRowViewModel>(InstallModpackAsync);

        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.SelectedVersionId) && !string.IsNullOrEmpty(_state.SelectedVersionId))
            {
                GameVersion = _state.SelectedVersionId!;
            }
        };
    }

    /// <summary>来源站。</summary>
    public ObservableCollection<string> Sources { get; } = ["Modrinth", "CurseForge"];

    /// <summary>资源种类。</summary>
    public ObservableCollection<string> Kinds { get; } = ["模组", "资源包", "光影", "数据包", "整合包"];

    /// <summary>加载器筛选。</summary>
    public ObservableCollection<string> Loaders { get; } = ["不限", "Fabric", "Forge", "NeoForge", "Quilt"];

    /// <summary>排序方式。</summary>
    public ObservableCollection<string> Sorts { get; } = ["相关度", "下载量", "最近更新", "最新发布"];

    /// <summary>推荐资源。</summary>
    public ObservableCollection<ResourceRowViewModel> Recommended { get; } = [];

    /// <summary>搜索结果。</summary>
    public ObservableCollection<ResourceRowViewModel> Results { get; } = [];

    private readonly List<ResourceRowViewModel> _allResults = [];

    /// <summary>结果列表变化事件。</summary>
    public event EventHandler? ResultsChanged;

    /// <summary>仅显示与当前游戏版本匹配的结果。</summary>
    [ObservableProperty]
    private bool _matchCurrentVersion;

    /// <summary>来源。</summary>
    [ObservableProperty]
    private string _source = "Modrinth";

    /// <summary>种类。</summary>
    [ObservableProperty]
    private string _kind = "模组";

    /// <summary>加载器。</summary>
    [ObservableProperty]
    private string _loader = "不限";

    /// <summary>排序。</summary>
    [ObservableProperty]
    private string _sort = "相关度";

    /// <summary>游戏版本。</summary>
    [ObservableProperty]
    private string _gameVersion;

    /// <summary>关键字。</summary>
    [ObservableProperty]
    private string _query = string.Empty;

    /// <summary>状态。</summary>
    [ObservableProperty]
    private string _status = "输入关键字后点击“搜索”，或直接查看上方的推荐资源。";

    /// <summary>进度。</summary>
    [ObservableProperty]
    private double _progress;

    /// <summary>推荐加载状态。</summary>
    [ObservableProperty]
    private string _recommendedStatus = "点击“加载推荐”获取热门模组与整合包。";

    /// <summary>密钥提示。</summary>
    public string KeyHint => _services.HasCurseForgeKey
        ? "CurseForge：已配置 API Key"
        : "CurseForge：未配置 API Key（放一份 ACL.secrets.json 到启动器目录）";

    /// <summary>安装命令。</summary>
    public ICommand InstallCommand { get; }

    /// <summary>整合包安装命令。</summary>
    public ICommand InstallPackCommand { get; }


    partial void OnMatchCurrentVersionChanged(bool value) => ApplyResultFilter();
    partial void OnKindChanged(string value) => ApplyResultFilter();

    /// <summary>加载推荐资源（热门模组 + 热门整合包）。</summary>
    [RelayCommand]
    private async Task LoadRecommendedAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        RecommendedStatus = "正在获取推荐资源…";
        try
        {
            Recommended.Clear();
            _recommended.Clear();
            string? version = string.IsNullOrWhiteSpace(GameVersion) ? null : GameVersion;

            foreach ((ResourceKind kind, string title) in new[]
            {
                (ResourceKind.Modpack, "热门整合包"),
                (ResourceKind.Mod, "热门模组"),
                (ResourceKind.ShaderPack, "热门光影"),
            })
            {
                Result<ResourceSearchResult> result = await _services.Modrinth
                    .SearchAsync(new ResourceSearchQuery(string.Empty, kind, version, null, 0, 6, ResourceSortField.Downloads))
                    .ConfigureAwait(true);
                if (!result.IsSuccess)
                {
                    continue;
                }

                foreach (ResourceProject project in result.Value!.Projects)
                {
                    _recommended.Add(new ResourceRowViewModel(project) { Install = kind == ResourceKind.Modpack ? InstallPackCommand : InstallCommand });
                }

                RecommendedStatus = "推荐资源已加载。";
            }

            ApplyRecommended(kind => true);
            await LoadIconsAsync(_recommended).ConfigureAwait(true);
            RecommendedStatus = _recommended.Count == 0 ? "未能获取推荐资源（请检查网络）。" : $"已加载 {_recommended.Count} 条推荐资源。";
        }
        catch (Exception ex)
        {
            RecommendedStatus = $"加载推荐失败：{ex.GetType().Name}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyRecommended(Func<ResourceProject, bool> predicate)
    {
        Recommended.Clear();
        foreach (ResourceRowViewModel row in _recommended.Where(r => predicate(r.Project)))
        {
            Recommended.Add(row);
        }
    }

    /// <summary>搜索。</summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Query))
        {
            Status = "请输入关键字。";
            return;
        }

        IsBusy = true;
        Status = $"正在 {Source} 搜索…";
        Results.Clear();
        try
        {
            ResourceSearchQuery query = new(
                Query,
                CurrentKind(),
                string.IsNullOrWhiteSpace(GameVersion) ? null : GameVersion,
                CurrentLoader(),
                0,
                30,
                CurrentSort());

            Result<ResourceSearchResult> result = await CurrentIndex().SearchAsync(query).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                Status = $"搜索失败：{result.Code}（{result.Detail}）";
                return;
            }

            _allResults.Clear();
            foreach (ResourceProject project in result.Value!.Projects)
            {
                _allResults.Add(new ResourceRowViewModel(project)
                {
                    Install = project.Kind == ResourceKind.Modpack ? InstallPackCommand : InstallCommand,
                });
            }

            ApplyResultFilter();
            ResultsChanged?.Invoke(this, EventArgs.Empty);
            Status = $"共 {result.Value!.Total} 条结果，显示 {Results.Count} 条。";
            await LoadIconsAsync(_allResults).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Status = $"搜索失败：{ex.GetType().Name}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>安装资源（下载到游戏目录对应子目录）。</summary>
    private async Task InstallAsync(ResourceRowViewModel? row)
    {
        if (row is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        row.Status = "获取文件…";
        Progress = 0;
        try
        {
            Result<IReadOnlyList<ResourceFile>> files = await CurrentIndex()
                .GetFilesAsync(row.Project.Id, string.IsNullOrWhiteSpace(GameVersion) ? null : GameVersion, CurrentLoader())
                .ConfigureAwait(true);
            if (!files.IsSuccess || files.Value!.Count == 0)
            {
                row.Status = files.IsSuccess ? "无可用文件" : $"失败：{files.Code}";
                return;
            }

            ResourceFile file = files.Value[0];
            string folder = FolderFor(row.Project.Kind);
            string target = Path.Combine(_services.GameDirectory, folder, file.FileName);
            UiProgressSink sink = new(report => Progress = report.Fraction);

            Result<Core.Download.DownloadOutcome> downloaded = await _services.Download
                .DownloadAsync(new DownloadItem(file.Url, [], target, file.PreferredHash, file.Size), sink)
                .ConfigureAwait(true);
            row.Status = downloaded.IsSuccess ? $"已下载到 {folder}/" : $"失败：{downloaded.Code}";
            Status = row.Status;
        }
        catch (Exception ex)
        {
            row.Status = $"失败：{ex.GetType().Name}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>直接下载整合包并安装，随后进入游戏列表。</summary>
    private async Task InstallModpackAsync(ResourceRowViewModel? row)
    {
        if (row is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        row.Status = "解析整合包…";
        Progress = 0;
        try
        {
            Result<IReadOnlyList<ResourceFile>> files = await CurrentIndex()
                .GetFilesAsync(row.Project.Id, string.IsNullOrWhiteSpace(GameVersion) ? null : GameVersion, null)
                .ConfigureAwait(true);
            if (!files.IsSuccess || files.Value!.Count == 0)
            {
                row.Status = files.IsSuccess ? "无可用整合包文件" : $"失败：{files.Code}";
                return;
            }

            ResourceFile file = files.Value[0];
            string archive = Path.Combine(_services.DataDirectory, "downloads", file.FileName);
            UiProgressSink sink = new(report => Progress = report.Fraction * 0.6);
            Result<Core.Download.DownloadOutcome> downloaded = await _services.Download
                .DownloadAsync(new DownloadItem(file.Url, [], archive, file.PreferredHash, file.Size), sink)
                .ConfigureAwait(true);
            if (!downloaded.IsSuccess)
            {
                row.Status = $"下载失败：{downloaded.Code}";
                return;
            }

            row.Status = "安装中…";
            Result<ModpackManifest> parsed = _services.Modpack.Parse(archive);
            if (!parsed.IsSuccess)
            {
                row.Status = $"解析失败：{parsed.Code}";
                return;
            }

            Result<ModpackInstallResult> installed = await _services.Modpack
                .InstallAsync(parsed.Value!, archive, _services.GameDirectory, sink)
                .ConfigureAwait(true);
            if (!installed.IsSuccess)
            {
                row.Status = $"安装失败：{installed.Code}";
                return;
            }

            Progress = 1;
            row.Status = "整合包已安装，正在准备实例…";
            await EnsureModpackInstanceAsync(parsed.Value!, installed.Value!).ConfigureAwait(true);
            row.Status = $"已安装 {parsed.Value!.Name}";
            Status = row.Status;
            _host.RefreshInstances();
            _host.ShowToast($"整合包 {parsed.Value!.Name} 已就绪，可在“游戏列表”启动");
            _host.ShowPage("games");
        }
        catch (Exception ex)
        {
            row.Status = $"失败：{ex.GetType().Name}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>为整合包准备可启动实例：补齐基础版本，必要时安装其加载器。</summary>
    private async Task EnsureModpackInstanceAsync(ModpackManifest manifest, ModpackInstallResult result)
    {
        ModpackLoader? loader = manifest.Loaders.FirstOrDefault();
        string instanceName = loader is { } required
            ? $"{manifest.MinecraftVersion}-{required.Kind.ToString().ToLowerInvariant()}-{required.Version}"
            : manifest.MinecraftVersion;

        UiProgressSink sink = new(report =>
        {
            Progress = 0.6 + (report.Fraction * 0.4);
            Status = $"准备实例：{report.Stage}";
        });

        await _services.LoaderInstaller
            .InstallAsync(
                new Core.Version.Loaders.LoaderInstallRequest(
                    loader?.Kind ?? ModLoaderKind.None,
                    manifest.MinecraftVersion,
                    loader?.Version,
                    instanceName,
                    _services.GameDirectory),
                sink)
            .ConfigureAwait(true);
    }

    private void ApplyResultFilter()
    {
        Results.Clear();
        string? version = string.IsNullOrWhiteSpace(GameVersion) ? null : GameVersion;
        foreach (ResourceRowViewModel row in _allResults)
        {
            if (MatchCurrentVersion && version is not null
                && !row.Project.GameVersions.Any(v => string.Equals(v, version, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            Results.Add(row);
        }

        ResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadIconsAsync(IEnumerable<ResourceRowViewModel> rows)
    {
        foreach (ResourceRowViewModel row in rows)
        {
            if (row.Icon is not null || string.IsNullOrWhiteSpace(row.Project.IconUrl)
                || !Uri.TryCreate(row.Project.IconUrl, UriKind.Absolute, out Uri? uri))
            {
                continue;
            }

            try
            {
                Result<HttpResponse> sent = await _services.Http.SendAsync(new HttpRequest(uri)).ConfigureAwait(true);
                if (!sent.IsSuccess || sent.Value is null)
                {
                    continue;
                }

                using HttpResponse response = sent.Value;
                if (response.Content is null)
                {
                    continue;
                }

                using MemoryStream buffer = new();
                await response.Content.CopyToAsync(buffer).ConfigureAwait(true);
                buffer.Position = 0;
                Avalonia.Media.Imaging.Bitmap bitmap = new(buffer);
                Dispatcher.UIThread.Post(() => row.Icon = bitmap);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
            {
                // 图标失败不影响检索。
            }
        }
    }

    private IResourceIndex CurrentIndex() => Source == "CurseForge" ? _services.CurseForge : _services.Modrinth;

    private ResourceKind CurrentKind() => Kind switch
    {
        "资源包" => ResourceKind.ResourcePack,
        "光影" => ResourceKind.ShaderPack,
        "数据包" => ResourceKind.DataPack,
        "整合包" => ResourceKind.Modpack,
        _ => ResourceKind.Mod,
    };

    private ResourceSortField CurrentSort() => Sort switch
    {
        "下载量" => ResourceSortField.Downloads,
        "最近更新" => ResourceSortField.Updated,
        "最新发布" => ResourceSortField.Newest,
        _ => ResourceSortField.Relevance,
    };

    private ModLoaderKind? CurrentLoader() => Loader switch
    {
        "Fabric" => ModLoaderKind.Fabric,
        "Forge" => ModLoaderKind.Forge,
        "NeoForge" => ModLoaderKind.NeoForge,
        "Quilt" => ModLoaderKind.Quilt,
        _ => null,
    };

    private static string FolderFor(ResourceKind kind) => kind switch
    {
        ResourceKind.ResourcePack => "resourcepacks",
        ResourceKind.ShaderPack => "shaderpacks",
        ResourceKind.DataPack => "datapacks",
        ResourceKind.Modpack => "modpacks",
        _ => "mods",
    };
}