using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Download;

namespace ACL.Core.Version.Loaders;

/// <summary>
/// 默认加载器安装器（完全自研实现）：
/// <list type="bullet">
///   <item>原版：拉取版本 JSON → 落盘 → 补齐客户端 / 库 / 资源索引。</item>
///   <item>Fabric / Quilt：拉取官方 install profile → 重写为本地实例 JSON → 补齐依赖。</item>
///   <item>Forge / NeoForge：下载官方安装器并以 <c>--installClient</c> 无交互执行，随后校验产出的实例。</item>
///   <item>OptiFine：下载补丁包并按 Maven 布局落盘，生成"继承基础版本"的实例 JSON。</item>
/// </list>
/// </summary>
public sealed class LoaderInstaller : ILoaderInstaller
{
    private readonly IHttpTransport _http;
    private readonly IFileSystem _fileSystem;
    private readonly IDownloadService _download;
    private readonly IProcessLauncher _process;
    private readonly ILoaderCatalog _catalog;
    private readonly IVersionCatalog _versions;
    private readonly OsSpec _os;

    /// <summary>构造安装器。</summary>
    public LoaderInstaller(
        IVersionCatalog versions,
        IHttpTransport http,
        IFileSystem fileSystem,
        IDownloadService download,
        IProcessLauncher process,
        ILoaderCatalog catalog,
        OsSpec? os = null)
    {
        _versions = versions ?? throw new ArgumentNullException(nameof(versions));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _download = download ?? throw new ArgumentNullException(nameof(download));
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _os = os ?? CurrentOs();
    }

    /// <inheritdoc />
    public async Task<Result<LoaderInstallResult>> InstallAsync(
        LoaderInstallRequest request,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        InstallTargets targets = InstallTargets.ForGameDir(request.GameDir);

        try
        {
            return request.Kind switch
            {
                ModLoaderKind.None => await InstallVanillaAsync(request, targets, progress, cancellationToken).ConfigureAwait(false),
                ModLoaderKind.Fabric or ModLoaderKind.Quilt => await InstallProfileLoaderAsync(request, targets, progress, cancellationToken).ConfigureAwait(false),
                ModLoaderKind.Forge or ModLoaderKind.NeoForge => await InstallOfficialInstallerAsync(request, targets, progress, cancellationToken).ConfigureAwait(false),
                ModLoaderKind.OptiFine => await InstallOptiFineAsync(request, targets, progress, cancellationToken).ConfigureAwait(false),
                _ => Result<LoaderInstallResult>.Failure(ErrorCode.Unsupported, $"{request.Kind} 暂不支持自动安装。"),
            };
        }
        catch (OperationCanceledException)
        {
            return Result<LoaderInstallResult>.Failure(ErrorCode.Canceled, "已取消。");
        }
        catch (IOException ex)
        {
            return Result<LoaderInstallResult>.Failure(ErrorCode.IoError, ex.Message);
        }
    }

    private async Task<Result<LoaderInstallResult>> InstallVanillaAsync(
        LoaderInstallRequest request,
        InstallTargets targets,
        IProgressSink? progress,
        CancellationToken cancellationToken)
    {
        VersionSource source = new(_versions, HttpOf(), _fileSystem, targets.VersionsDir);
        progress?.Report("读取版本 JSON", 0.05, request.GameVersion);
        Result<VersionJson> version = await source.LoadAsync(request.GameVersion, cancellationToken).ConfigureAwait(false);
        if (!version.IsSuccess)
        {
            return Result<LoaderInstallResult>.Failure(version.Code, version.Detail);
        }

        return await InstallFromVersionAsync(request, targets, version.Value!, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<LoaderInstallResult>> InstallFromVersionAsync(
        LoaderInstallRequest request,
        InstallTargets targets,
        VersionJson version,
        IProgressSink? progress,
        CancellationToken cancellationToken)
    {
        VersionJson instance = version with { Id = request.InstanceName };
        VersionSource source = new(_versions, _http, _fileSystem, targets.VersionsDir);
        Result saved = source.SaveLocal(request.InstanceName, VersionJsonWriter.Write(instance));
        if (!saved.IsSuccess)
        {
            return Result<LoaderInstallResult>.Failure(saved.Code, saved.Detail);
        }

        List<string> notes = [$"已写入实例 {request.InstanceName}（基于 {request.GameVersion}）。"];
        return await CompleteAsync(request, targets, [request.InstanceName], notes, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<LoaderInstallResult>> InstallProfileLoaderAsync(
        LoaderInstallRequest request,
        InstallTargets targets,
        IProgressSink? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LoaderVersion))
        {
            return Result<LoaderInstallResult>.Failure(ErrorCode.NotFound, "缺少加载器版本。");
        }

        // Fabric / Quilt 的 install profile 是一份完整的版本 JSON。
        string baseUrl = request.Kind == ModLoaderKind.Fabric
            ? "https://meta.fabricmc.net/v2/versions/loader"
            : "https://meta.quiltmc.org/v3/versions/loader";
        Uri profile = new($"{baseUrl}/{Uri.EscapeDataString(request.GameVersion)}/{Uri.EscapeDataString(request.LoaderVersion)}/profile/json");
        MirrorKind mirrorKind = request.Kind == ModLoaderKind.Fabric ? MirrorKind.FabricMeta : MirrorKind.QuiltMeta;

        progress?.Report("获取加载器 profile", 0.10, profile.ToString());
        MetadataFetcher fetcher = new(HttpOf());
        Result<string> body = await fetcher.FetchStringAsync(new MirrorPolicy().Resolve(mirrorKind, profile), cancellationToken).ConfigureAwait(false);
        if (!body.IsSuccess)
        {
            return Result<LoaderInstallResult>.Failure(body.Code, body.Detail);
        }

        Result<VersionJson> parsed = VersionJsonParser.Parse(body.Value!);
        if (!parsed.IsSuccess)
        {
            return Result<LoaderInstallResult>.Failure(parsed.Code, parsed.Detail);
        }

        // 用请求的实例名重写 id，并确保继承基础版本。
        VersionJson instance = parsed.Value! with
        {
            Id = request.InstanceName,
            InheritsFrom = parsed.Value!.InheritsFrom ?? request.GameVersion,
        };

        VersionSource source = new(_versions, HttpOf(), _fileSystem, targets.VersionsDir);
        Result saved = source.SaveLocal(request.InstanceName, VersionJsonWriter.Write(instance));
        if (!saved.IsSuccess)
        {
            return Result<LoaderInstallResult>.Failure(saved.Code, saved.Detail);
        }

        List<string> notes = [$"{ModLoaderNames.Display(request.Kind)} {request.LoaderVersion} 安装 profile 已写入本地。"];
        return await CompleteAsync(request, targets, [request.InstanceName], notes, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<LoaderInstallResult>> InstallOfficialInstallerAsync(
        LoaderInstallRequest request,
        InstallTargets targets,
        IProgressSink? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LoaderVersion))
        {
            return Result<LoaderInstallResult>.Failure(ErrorCode.NotFound, "缺少加载器版本。");
        }

        if (string.IsNullOrWhiteSpace(request.JavaPath))
        {
            return Result<LoaderInstallResult>.Failure(ErrorCode.NotFound, "运行官方安装器需要指定 Java（请在“设置”页探测或指定）。");
        }

        // 官方安装器要求基础版本已安装到目标目录。
        Result<LoaderInstallResult> vanilla = await InstallVanillaAsync(request with { Kind = ModLoaderKind.None, InstanceName = request.GameVersion }, targets, progress, cancellationToken).ConfigureAwait(false);
        if (!vanilla.IsSuccess)
        {
            return vanilla;
        }

        (Uri installerUri, string fileName) = InstallerUri(request);
        string installerDir = Path.Combine(request.GameDir, "installers");
        string installerPath = Path.Combine(installerDir, fileName);
        progress?.Report("下载官方安装器", 0.20, fileName);

        DownloadItem item = new(installerUri, new MirrorPolicy().Resolve(MirrorKind.Library, installerUri), installerPath);
        Result<DownloadOutcome> fetched = await _download.DownloadAsync(item, progress, cancellationToken).ConfigureAwait(false);
        if (!fetched.IsSuccess)
        {
            return Result<LoaderInstallResult>.Failure(fetched.Code, fetched.Detail);
        }

        HashSet<string> before = SnapshotVersions(targets.VersionsDir);
        progress?.Report("运行官方安装器", 0.45, fileName);
        ProcessSpec spec = new(request.JavaPath!, ["-jar", installerPath, "--installClient", request.GameDir], request.GameDir);
        Result<ProcessResult> run = await _process.RunAsync(spec, cancellationToken).ConfigureAwait(false);
        if (!run.IsSuccess)
        {
            return Result<LoaderInstallResult>.Failure(run.Code, run.Detail);
        }

        if (run.Value!.ExitCode != 0)
        {
            return Result<LoaderInstallResult>.Failure(
                ErrorCode.IoError,
                $"官方安装器退出码 {run.Value.ExitCode}。{Tail(run.Value.CombinedOutput)}");
        }

        List<string> created = [.. SnapshotVersions(targets.VersionsDir).Except(before).OrderBy(name => name, StringComparer.Ordinal)];
        if (created.Count == 0)
        {
            return Result<LoaderInstallResult>.Failure(ErrorCode.NotFound, "安装器未产出新实例（请检查日志）。");
        }

        string instanceId = created.FirstOrDefault(name => name.Contains(request.LoaderVersion!, StringComparison.OrdinalIgnoreCase)) ?? created[0];
        List<string> notes = [$"官方安装器已产出实例：{string.Join("、", created)}。"];
        return await CompleteAsync(request, targets, created, notes, progress, cancellationToken, instanceId).ConfigureAwait(false);
    }

    private async Task<Result<LoaderInstallResult>> InstallOptiFineAsync(
        LoaderInstallRequest request,
        InstallTargets targets,
        IProgressSink? progress,
        CancellationToken cancellationToken)
    {

        Uri? uri = request.DownloadUri;
        if (uri is null && string.IsNullOrWhiteSpace(request.LoaderVersion))
        {
            return Result<LoaderInstallResult>.Failure(ErrorCode.NotFound, "缺少 OptiFine 版本。");
        }

        if (uri is null)
        {
            Result<IReadOnlyList<LoaderOffer>> offers = await _catalog.GetVersionsAsync(ModLoaderKind.OptiFine, request.GameVersion, cancellationToken).ConfigureAwait(false);
            if (!offers.IsSuccess)
            {
                return Result<LoaderInstallResult>.Failure(offers.Code, offers.Detail);
            }

            uri = offers.Value!.FirstOrDefault(offer => offer.Version == request.LoaderVersion)?.DownloadUri;
            if (uri is null)
            {
                return Result<LoaderInstallResult>.Failure(ErrorCode.NotFound, "未找到对应的 OptiFine 安装包。");
            }
        }

        string artifact = request.LoaderVersion ?? Path.GetFileNameWithoutExtension(uri.LocalPath);
        string relative = $"optifine/OptiFine/{artifact}/OptiFine-{artifact}.jar";
        string libraryPath = Path.Combine(targets.LibrariesDir, relative.Replace('/', Path.DirectorySeparatorChar));

        progress?.Report("下载 OptiFine", 0.20, artifact);
        DownloadItem item = new(uri, [], libraryPath);
        Result<DownloadOutcome> fetched = await _download.DownloadAsync(item, progress, cancellationToken).ConfigureAwait(false);
        if (!fetched.IsSuccess)
        {
            return Result<LoaderInstallResult>.Failure(fetched.Code, fetched.Detail);
        }

        string parent = request.BaseInstanceId ?? request.GameVersion;
        VersionJson instance = new(
            request.InstanceName,
            parent,
            null,
            null,
            "release",
            null,
            null,
            null,
            [
                new LibraryRef(
                    $"optifine:OptiFine:{artifact}",
                    new LibraryDownloads(new DownloadArtifact(uri, null, null, relative))),
            ],
            [],
            [],
            null);

        VersionSource source = new(_versions, HttpOf(), _fileSystem, targets.VersionsDir);
        Result saved = source.SaveLocal(request.InstanceName, VersionJsonWriter.Write(instance));
        if (!saved.IsSuccess)
        {
            return Result<LoaderInstallResult>.Failure(saved.Code, saved.Detail);
        }

        List<string> notes =
        [
            $"OptiFine {artifact} 已安装为独立补丁实例（继承 {parent}）。",
            "现代版本建议把 OptiFine 叠加在 Forge / NeoForge 实例上，兼容性更好。",
        ];

        return await CompleteAsync(request, targets, [request.InstanceName], notes, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<LoaderInstallResult>> CompleteAsync(
        LoaderInstallRequest request,
        InstallTargets targets,
        IReadOnlyList<string> created,
        IReadOnlyList<string> notes,
        IProgressSink? progress,
        CancellationToken cancellationToken,
        string? instanceId = null)
    {
        string id = instanceId ?? request.InstanceName;
        VersionSource source = new(_versions, HttpOf(), _fileSystem, targets.VersionsDir);
        progress?.Report("合并版本链", 0.60, id);
        Result<VersionJson> merged = await source.ResolveAsync(id, cancellationToken).ConfigureAwait(false);
        if (!merged.IsSuccess)
        {
            return Result<LoaderInstallResult>.Failure(merged.Code, merged.Detail);
        }

        InstallPlan plan = new VersionPlanner().Plan(merged.Value!, targets, _os);
        progress?.Report("补齐文件", 0.65, $"{plan.Downloads.Count} 项");

        Result<IReadOnlyList<DownloadOutcome>> downloads = await _download
            .DownloadManyAsync(plan.Downloads, 4, progress, cancellationToken)
            .ConfigureAwait(false);
        if (!downloads.IsSuccess)
        {
            return Result<LoaderInstallResult>.Failure(downloads.Code, downloads.Detail);
        }

        List<string> allNotes = [.. notes, .. plan.Warnings.Select(warning => "警告：" + warning)];
        progress?.Report("完成", 1.0, id);
        return Result<LoaderInstallResult>.Success(new LoaderInstallResult(
            id,
            source.LocalJsonPath(id),
            created,
            downloads.Value!,
            allNotes));
    }

    private static (Uri Uri, string FileName) InstallerUri(LoaderInstallRequest request)
    {
        string loader = request.LoaderVersion!;
        if (request.Kind == ModLoaderKind.Forge)
        {
            string version = $"{request.GameVersion}-{loader}";
            string file = $"forge-{version}-installer.jar";
            return (new Uri($"https://maven.minecraftforge.net/net/minecraftforge/forge/{version}/{file}"), file);
        }

        bool legacy = request.GameVersion.StartsWith("1.20.1", StringComparison.Ordinal);
        if (legacy)
        {
            string version = $"{request.GameVersion}-{loader}";
            string file = $"forge-{version}-installer.jar";
            return (new Uri($"https://maven.neoforged.net/releases/net/neoforged/forge/{version}/{file}"), file);
        }

        string neoforgeFile = $"neoforge-{loader}-installer.jar";
        return (new Uri($"https://maven.neoforged.net/releases/net/neoforged/neoforge/{loader}/{neoforgeFile}"), neoforgeFile);
    }

    private HashSet<string> SnapshotVersions(string versionsDir)
    {
        HashSet<string> set = new(StringComparer.Ordinal);
        if (!_fileSystem.DirectoryExists(versionsDir))
        {
            return set;
        }

        string normalized = versionsDir.Replace('\\', '/').TrimEnd('/');
        foreach (string dir in _fileSystem.EnumerateDirectories(versionsDir))
        {
            string candidate = dir.Replace('\\', '/').TrimEnd('/');
            if (candidate.Length <= normalized.Length + 1 || !candidate.StartsWith(normalized + "/", StringComparison.Ordinal)
                || candidate[(normalized.Length + 1)..].Contains('/'))
            {
                continue;
            }

            set.Add(Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        }

        return set;
    }

    private static string Tail(string text, int max = 400)
        => text.Length <= max ? text : text[^max..];

    private IHttpTransport HttpOf() => _http;

    private static OsSpec CurrentOs()
        => OperatingSystem.IsWindows() ? OsSpec.WindowsX64
            : OperatingSystem.IsMacOS() ? OsSpec.MacOsX64
            : OsSpec.LinuxX64;
}