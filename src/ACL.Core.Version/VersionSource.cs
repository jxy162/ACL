using System.Text;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>
/// 版本 JSON 的统一来源：先查本地 <c>&lt;versions&gt;/&lt;id&gt;/&lt;id&gt;.json</c>，
/// 再退回到官方清单（且支持 <c>inheritsFrom</c> 链合并）。
/// </summary>
public sealed class VersionSource
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IVersionCatalog _catalog;
    private readonly MetadataFetcher _fetcher;
    private readonly IFileSystem _fileSystem;
    private readonly string _versionsDir;

    /// <summary>构造版本来源。</summary>
    public VersionSource(IVersionCatalog catalog, IHttpTransport http, IFileSystem fileSystem, string versionsDir)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _fetcher = new MetadataFetcher(http ?? throw new ArgumentNullException(nameof(http)));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        ArgumentException.ThrowIfNullOrWhiteSpace(versionsDir);
        _versionsDir = versionsDir;
    }

    /// <summary>本地版本 JSON 路径。</summary>
    public string LocalJsonPath(string id) => Path.Combine(_versionsDir, id, id + ".json");

    /// <summary>读取某版本 JSON（本地优先）。</summary>
    public async Task<Result<VersionJson>> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        Result<VersionJson> local = TryLoadLocal(id);
        if (local.IsSuccess || local.Code != ErrorCode.NotFound)
        {
            return local;
        }

        Result<VersionManifest> manifest = await _catalog.GetManifestAsync(cancellationToken).ConfigureAwait(false);
        if (!manifest.IsSuccess)
        {
            return Result<VersionJson>.Failure(manifest.Code, manifest.Detail);
        }

        VersionEntry? entry = manifest.Value!.Versions
            .FirstOrDefault(v => string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return Result<VersionJson>.Failure(ErrorCode.NotFound, id);
        }

        Result<string> body = await _fetcher.FetchStringAsync([entry.Url], cancellationToken).ConfigureAwait(false);
        return body.IsSuccess
            ? VersionJsonParser.Parse(body.Value!)
            : Result<VersionJson>.Failure(body.Code, body.Detail);
    }

    /// <summary>读取并合并 <c>inheritsFrom</c> 链。</summary>
    public Task<Result<VersionJson>> ResolveAsync(string id, CancellationToken cancellationToken = default)
        => VersionResolver.ResolveAsync(id, (current, ct) => LoadAsync(current, ct), cancellationToken);

    /// <summary>尝试读取本地版本 JSON。</summary>
    public Result<VersionJson> TryLoadLocal(string id)
    {
        string path = LocalJsonPath(id);
        if (!_fileSystem.FileExists(path))
        {
            return Result<VersionJson>.Failure(ErrorCode.NotFound, path);
        }

        try
        {
            using Stream stream = _fileSystem.OpenRead(path);
            using StreamReader reader = new(stream, Utf8NoBom);
            return VersionJsonParser.Parse(reader.ReadToEnd());
        }
        catch (IOException ex)
        {
            return Result<VersionJson>.Failure(ErrorCode.IoError, ex.Message);
        }
    }

    /// <summary>写出本地版本 JSON（自动创建目录）。</summary>
    public Result SaveLocal(string id, string json)
    {
        string path = LocalJsonPath(id);
        try
        {
            _fileSystem.CreateDirectory(Path.Combine(_versionsDir, id));
            using Stream stream = _fileSystem.OpenWrite(path, append: false);
            using StreamWriter writer = new(stream, Utf8NoBom);
            writer.Write(json);
            return Result.Ok;
        }
        catch (IOException ex)
        {
            return Result.Failure(ErrorCode.IoError, ex.Message);
        }
    }
}