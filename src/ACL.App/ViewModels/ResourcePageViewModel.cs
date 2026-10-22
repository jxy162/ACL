using System.Collections.ObjectModel;
using ACL.App.Services;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Download;
using ACL.Core.Resources;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>资源页：检索并安装 Mod / 资源包 / 光影 / 数据包。</summary>
public partial class ResourcePageViewModel : PageViewModelBase
{
    private readonly AppServices _services;
    private readonly AppState _state;

    /// <summary>构造资源页。</summary>
    public ResourcePageViewModel(AppServices services, AppState state)
        : base("资源", "检索并一键安装 Mod / 资源包 / 光影 / 数据包（Modrinth 免鉴权；CurseForge 需 API Key）。")
    {
        _services = services;
        _state = state;
        _gameVersion = state.SelectedVersionId ?? string.Empty;

        // 跟随“游戏版本”选择联动检索用的版本。
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
    public ObservableCollection<string> Kinds { get; } = ["模组", "资源包", "光影", "数据包"];

    /// <summary>加载器筛选。</summary>
    public ObservableCollection<string> Loaders { get; } = ["不限", "Fabric", "Forge", "NeoForge", "Quilt"];

    /// <summary>排序方式。</summary>
    public ObservableCollection<string> Sorts { get; } = ["相关度", "下载量", "最近更新", "最新发布"];

    /// <summary>搜索结果（已按筛选条件过滤）。</summary>
    public ObservableCollection<ResourceRowViewModel> Results { get; } = [];

    private readonly List<ResourceRowViewModel> _allResults = [];

    /// <summary>仅显示与当前游戏版本匹配的结果。</summary>
    [ObservableProperty]
    private bool _matchCurrentVersion;

    partial void OnMatchCurrentVersionChanged(bool value) => ApplyResultFilter();

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
    private string _status = "输入关键字后点击“搜索”。";

    /// <summary>进度。</summary>
    [ObservableProperty]
    private double _progress;

    /// <summary>密钥提示。</summary>
    public string KeyHint => _services.HasCurseForgeKey
        ? "CurseForge：已配置 API Key"
        : "CurseForge：未配置 API Key（放一份 ACL.secrets.json 到启动器目录，或设置 ACL_/PCL_CURSEFORGE_API_KEY）";

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
                _allResults.Add(new ResourceRowViewModel(project) { Install = InstallCommand });
            }

            ApplyResultFilter();
            Status = $"共 {result.Value!.Total} 条结果，已显示 {Results.Count} 条。";
            _ = LoadIconsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>安装（下载到游戏目录对应子目录）。</summary>
    [RelayCommand]
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
            if (!files.IsSuccess)
            {
                row.Status = $"失败：{files.Code}";
                return;
            }

            ResourceFile? file = files.Value!.FirstOrDefault();
            if (file is null)
            {
                row.Status = "无可用文件";
                return;
            }

            string folder = FolderFor(CurrentKind());
            string target = Path.Combine(_services.GameDirectory, folder, file.FileName);
            DownloadItem item = new(file.Url, [], target, file.PreferredHash, file.Size);
            UiProgressSink sink = new(report => Progress = report.Fraction);

            Result<DownloadOutcome> downloaded = await _services.Download.DownloadAsync(item, sink).ConfigureAwait(true);
            row.Status = downloaded.IsSuccess ? $"已下载到 {folder}/" : $"失败：{downloaded.Code}";
            Status = row.Status;
        }
        finally
        {
            IsBusy = false;
        }
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
    }

    /// <summary>并发拉取图标缩略图（失败忽略）。</summary>
    private async Task LoadIconsAsync()
    {
        foreach (ResourceRowViewModel row in _allResults)
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
                // 图标加载失败不影响检索。
            }
        }
    }

    private IResourceIndex CurrentIndex() => Source == "CurseForge" ? _services.CurseForge : _services.Modrinth;

    private ResourceKind CurrentKind() => Kind switch
    {
        "资源包" => ResourceKind.ResourcePack,
        "光影" => ResourceKind.ShaderPack,
        "数据包" => ResourceKind.DataPack,
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