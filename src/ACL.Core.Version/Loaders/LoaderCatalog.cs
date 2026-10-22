using System.Text.Json;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Download;

namespace ACL.Core.Version.Loaders;

/// <summary>加载器版本目录：按 Minecraft 版本列举可安装的加载器 / 补丁版本。</summary>
public interface ILoaderCatalog
{
    /// <summary>列举某 Minecraft 版本下可用的加载器版本（新版本在前）。</summary>
    Task<Result<IReadOnlyList<LoaderOffer>>> GetVersionsAsync(
        ModLoaderKind kind,
        string gameVersion,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认加载器目录：官方元数据为主、BMCLAPI 镜像为备（经 <see cref="IMirrorPolicy"/> 解析候选源）。
/// 支持 Forge / NeoForge / Fabric / Quilt / OptiFine。
/// </summary>
public sealed class LoaderCatalog : ILoaderCatalog
{
    private readonly MetadataFetcher _fetcher;
    private readonly IMirrorPolicy _policy;

    /// <summary>构造加载器目录。</summary>
    public LoaderCatalog(IHttpTransport http, IMirrorPolicy? mirrorPolicy = null)
    {
        _fetcher = new MetadataFetcher(http ?? throw new ArgumentNullException(nameof(http)));
        _policy = mirrorPolicy ?? new MirrorPolicy();
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<LoaderOffer>>> GetVersionsAsync(
        ModLoaderKind kind,
        string gameVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);

        return kind switch
        {
            ModLoaderKind.Forge => GetForgeAsync(gameVersion, cancellationToken),
            ModLoaderKind.NeoForge => GetNeoForgeAsync(gameVersion, cancellationToken),
            ModLoaderKind.Fabric => GetMetaLoaderAsync(
                MirrorKind.FabricMeta,
                new Uri($"https://meta.fabricmc.net/v2/versions/loader/{Uri.EscapeDataString(gameVersion)}"),
                kind, gameVersion, cancellationToken),
            ModLoaderKind.Quilt => GetMetaLoaderAsync(
                MirrorKind.QuiltMeta,
                new Uri($"https://meta.quiltmc.org/v3/versions/loader/{Uri.EscapeDataString(gameVersion)}"),
                kind, gameVersion, cancellationToken),
            ModLoaderKind.OptiFine => GetOptiFineAsync(gameVersion, cancellationToken),
            _ => Task.FromResult(Result<IReadOnlyList<LoaderOffer>>.Success([])),
        };
    }

    private async Task<Result<IReadOnlyList<LoaderOffer>>> GetForgeAsync(string gameVersion, CancellationToken cancellationToken)
    {
        Uri origin = new("https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml");
        Result<string> body = await FetchAsync(MirrorKind.ForgeMeta, origin, cancellationToken).ConfigureAwait(false);
        if (!body.IsSuccess)
        {
            return Result<IReadOnlyList<LoaderOffer>>.Failure(body.Code, body.Detail);
        }

        string prefix = gameVersion + "-";
        List<LoaderOffer> offers = [];
        foreach (string version in ParseXmlVersions(body.Value!))
        {
            if (version.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                offers.Add(new LoaderOffer(ModLoaderKind.Forge, version[prefix.Length..], gameVersion));
            }
        }

        return Result<IReadOnlyList<LoaderOffer>>.Success(OrderDescending(offers));
    }

    private async Task<Result<IReadOnlyList<LoaderOffer>>> GetNeoForgeAsync(string gameVersion, CancellationToken cancellationToken)
    {
        bool legacy = gameVersion.StartsWith("1.20.1", StringComparison.Ordinal);
        Uri origin = legacy
            ? new Uri("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge")
            : new Uri("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge");

        Result<string> body = await FetchAsync(MirrorKind.NeoForgeMeta, origin, cancellationToken).ConfigureAwait(false);
        if (!body.IsSuccess)
        {
            return Result<IReadOnlyList<LoaderOffer>>.Failure(body.Code, body.Detail);
        }

        List<string> versions = ParseJsonStringArray(body.Value!, "versions");
        List<LoaderOffer> offers = [];
        if (legacy)
        {
            string prefix = gameVersion + "-";
            foreach (string version in versions.Where(v => v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                offers.Add(new LoaderOffer(ModLoaderKind.NeoForge, version[prefix.Length..], gameVersion));
            }
        }
        else if (NeoForgePrefix(gameVersion) is { } mcPrefix)
        {
            foreach (string version in versions.Where(v => v.StartsWith(mcPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                offers.Add(new LoaderOffer(ModLoaderKind.NeoForge, version, gameVersion));
            }
        }

        return Result<IReadOnlyList<LoaderOffer>>.Success(OrderDescending(offers));
    }

    /// <summary>由 Minecraft 版本推导 NeoForge 的主次版本前缀（如 <c>1.21.1</c> → <c>21.1.</c>）。</summary>
    public static string? NeoForgePrefix(string gameVersion)
    {
        string[] parts = gameVersion.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        return parts.Length >= 3 ? $"{parts[1]}.{parts[2]}." : parts[1] + ".";
    }

    private async Task<Result<IReadOnlyList<LoaderOffer>>> GetMetaLoaderAsync(
        MirrorKind kind,
        Uri origin,
        ModLoaderKind loaderKind,
        string gameVersion,
        CancellationToken cancellationToken)
    {
        Result<string> body = await FetchAsync(kind, origin, cancellationToken).ConfigureAwait(false);
        if (!body.IsSuccess)
        {
            return Result<IReadOnlyList<LoaderOffer>>.Failure(body.Code, body.Detail);
        }

        List<LoaderOffer> offers = [];
        try
        {
            using JsonDocument document = JsonDocument.Parse(body.Value!);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in document.RootElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("loader", out JsonElement loader) || loader.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (GetString(loader, "version") is { Length: > 0 } version)
                    {
                        bool stable = loader.TryGetProperty("stable", out JsonElement stableElement)
                            && stableElement.ValueKind == JsonValueKind.True;
                        offers.Add(new LoaderOffer(loaderKind, version, gameVersion, stable));
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<LoaderOffer>>.Failure(ErrorCode.ManifestCorrupt, ex.Message);
        }

        return Result<IReadOnlyList<LoaderOffer>>.Success(offers);
    }

    private async Task<Result<IReadOnlyList<LoaderOffer>>> GetOptiFineAsync(string gameVersion, CancellationToken cancellationToken)
    {
        // OptiFine 无公开 JSON 清单，官方源为 HTML 页面；此处以 BMCLAPI 的结构化清单为主源。
        Uri mirror = new("https://bmclapi2.bangbang93.com/optifine/versionList");
        Result<string> body = await _fetcher.FetchStringAsync([mirror], cancellationToken).ConfigureAwait(false);
        if (!body.IsSuccess)
        {
            return Result<IReadOnlyList<LoaderOffer>>.Failure(body.Code, body.Detail);
        }

        List<LoaderOffer> offers = [];
        try
        {
            using JsonDocument document = JsonDocument.Parse(body.Value!);
            foreach (JsonElement item in document.RootElement.EnumerateArray())
            {
                string? mc = GetString(item, "mcversion");
                string? type = GetString(item, "type");
                string? patch = GetString(item, "patch");
                if (mc is null || type is null || patch is null || !string.Equals(mc, gameVersion, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string artifact = $"{mc}_{type}_{patch}";
                Uri download = new($"https://bmclapi2.bangbang93.com/optifine/{mc}/{type}/{patch}");
                offers.Add(new LoaderOffer(
                    ModLoaderKind.OptiFine,
                    artifact,
                    gameVersion,
                    IsPreview: patch.Contains("pre", StringComparison.OrdinalIgnoreCase),
                    DownloadUri: download,
                    Note: GetString(item, "forge") is { Length: > 0 } forge ? $"需要 Forge {forge}" : null));
            }
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<LoaderOffer>>.Failure(ErrorCode.ManifestCorrupt, ex.Message);
        }

        return Result<IReadOnlyList<LoaderOffer>>.Success(offers);
    }

    private async Task<Result<string>> FetchAsync(MirrorKind kind, Uri origin, CancellationToken cancellationToken)
    {
        IReadOnlyList<Uri> candidates = _policy.Resolve(kind, origin);
        return await _fetcher.FetchStringAsync(candidates, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<string> ParseXmlVersions(string xml)
    {
        const string open = "<version>";
        const string close = "</version>";
        int index = 0;
        while (true)
        {
            int start = xml.IndexOf(open, index, StringComparison.Ordinal);
            if (start < 0)
            {
                yield break;
            }

            int end = xml.IndexOf(close, start, StringComparison.Ordinal);
            if (end < 0)
            {
                yield break;
            }

            yield return xml[(start + open.Length)..end].Trim();
            index = end + close.Length;
        }
    }

    private static List<string> ParseJsonStringArray(string json, string property)
    {
        List<string> result = [];
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty(property, out JsonElement array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in array.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        result.Add(item.GetString()!);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // 交给调用方按空结果处理。
        }

        return result;
    }

    private static List<LoaderOffer> OrderDescending(List<LoaderOffer> offers)
        => [.. offers
            .OrderByDescending(offer => VersionParsing.Parse(offer.Version).Comparable)
            .ThenByDescending(offer => offer.Version, StringComparer.Ordinal)];

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}