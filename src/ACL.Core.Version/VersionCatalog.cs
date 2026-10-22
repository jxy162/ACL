using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Download;

namespace ACL.Core.Version;

/// <summary>
/// <see cref="IVersionCatalog"/> 的默认实现：经 <see cref="IMirrorPolicy"/> 解析候选源，
/// 由 <see cref="MetadataFetcher"/> 并发取先抓取，再解析为 <see cref="VersionManifest"/>。
/// </summary>
public sealed class VersionCatalog : IVersionCatalog
{
    /// <summary>官方版本清单地址（可被构造参数覆盖）。</summary>
    public static readonly Uri DefaultManifestUri = new("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json");

    private readonly MetadataFetcher _fetcher;
    private readonly IMirrorPolicy _mirrorPolicy;
    private readonly Uri _manifestUri;

    /// <summary>构造版本目录。</summary>
    /// <param name="http">HTTP 传输。</param>
    /// <param name="mirrorPolicy">镜像策略；为 <see langword="null"/> 时使用默认。</param>
    /// <param name="manifestUri">清单地址；为 <see langword="null"/> 时使用官方地址。</param>
    public VersionCatalog(IHttpTransport http, IMirrorPolicy? mirrorPolicy = null, Uri? manifestUri = null)
    {
        _fetcher = new MetadataFetcher(http);
        _mirrorPolicy = mirrorPolicy ?? new MirrorPolicy();
        _manifestUri = manifestUri ?? DefaultManifestUri;
    }

    /// <inheritdoc />
    public async Task<Result<VersionManifest>> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Uri> candidates = _mirrorPolicy.Resolve(MirrorKind.VersionManifest, _manifestUri);
        Result<string> body = await _fetcher.FetchStringAsync(candidates, cancellationToken).ConfigureAwait(false);
        if (!body.IsSuccess)
        {
            return Result<VersionManifest>.Failure(body.Code, body.Detail);
        }

        return VersionManifestParser.Parse(body.Value!);
    }
}