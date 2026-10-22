using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Download;

namespace ACL.Core.Modpack;

/// <summary><see cref="IModpackInstaller"/> 的默认实现。</summary>
public sealed class ModpackInstaller : IModpackInstaller
{
    private readonly IFileSystem _fileSystem;
    private readonly IDownloadService _downloadService;
    private readonly IReadOnlyList<IModpackFormatHandler> _handlers;

    /// <summary>构造安装器。</summary>
    /// <param name="fileSystem">文件系统。</param>
    /// <param name="downloadService">下载服务。</param>
    /// <param name="handlers">格式处理器；为 <see langword="null"/> 时使用默认（Modrinth / CurseForge / 通用）。</param>
    public ModpackInstaller(
        IFileSystem fileSystem,
        IDownloadService downloadService,
        IEnumerable<IModpackFormatHandler>? handlers = null)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
        _handlers = handlers is null
            ? [new ModrinthModpackHandler(), new CurseForgeModpackHandler(), new GenericModpackHandler()]
            : [.. handlers];
    }

    /// <inheritdoc />
    public Result<ModpackManifest> Parse(string archivePath)
    {
        try
        {
            using IModpackArchive archive = new ZipModpackArchive(_fileSystem, archivePath);
            foreach (IModpackFormatHandler handler in _handlers)
            {
                if (handler.CanHandle(archive))
                {
                    return handler.Parse(archive);
                }
            }

            return Result<ModpackManifest>.Failure(ErrorCode.ManifestCorrupt, "无法识别整合包格式。");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FileNotFoundException)
        {
            return Result<ModpackManifest>.Failure(ErrorCode.IoError, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result<ModpackInstallResult>> InstallAsync(
        ModpackManifest manifest,
        string archivePath,
        string instanceDirectory,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);

        // 1. 复制覆盖文件
        int copied = 0;
        try
        {
            using IModpackArchive archive = new ZipModpackArchive(_fileSystem, archivePath);
            Result<int> extracted = archive.ExtractTo(instanceDirectory, manifest.OverridesPrefix);
            if (!extracted.IsSuccess)
            {
                return Result<ModpackInstallResult>.Failure(extracted.Code, extracted.Detail);
            }

            copied = extracted.Value;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FileNotFoundException)
        {
            return Result<ModpackInstallResult>.Failure(ErrorCode.IoError, ex.Message);
        }

        // 2. 下载可解析文件
        List<DownloadItem> items = [];
        foreach (ModpackFile file in manifest.Files)
        {
            if (!file.IsResolved || string.IsNullOrEmpty(file.RelativePath))
            {
                continue;
            }

            string target = Path.Combine(instanceDirectory, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            items.Add(new DownloadItem(file.Downloads[0], [.. file.Downloads.Skip(1)], target, file.PreferredHash, file.Size));
        }

        List<DownloadOutcome> outcomes = [];
        if (items.Count > 0)
        {
            Result<IReadOnlyList<DownloadOutcome>> downloaded = await _downloadService
                .DownloadManyAsync(items, 0, progress, cancellationToken)
                .ConfigureAwait(false);
            if (!downloaded.IsSuccess)
            {
                return Result<ModpackInstallResult>.Failure(downloaded.Code, downloaded.Detail);
            }

            outcomes.AddRange(downloaded.Value!);
        }

        return Result<ModpackInstallResult>.Success(new ModpackInstallResult(copied, outcomes, manifest.UnresolvedFiles));
    }
}