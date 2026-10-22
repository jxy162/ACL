using System.Globalization;
using System.Text.Json;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Resources;

/// <summary>CurseForge API v1 客户端（需 <c>x-api-key</c>，经 <see cref="ISecretProvider"/> 注入）。</summary>
public sealed class CurseForgeClient : IResourceIndex
{
    /// <summary>API Key 的密钥键名。</summary>
    public const string ApiKeySecretName = "ACL_CURSEFORGE_API_KEY";

    /// <summary>官方基址。</summary>
    public static readonly Uri DefaultBaseUri = new("https://api.curseforge.com/v1/");

    private const string ForgeCdnBase = "https://edge.forgecdn.net/files/";

    private readonly IHttpTransport _http;
    private readonly ISecretProvider _secrets;
    private readonly Uri _baseUri;

    /// <summary>构造客户端。</summary>
    public CurseForgeClient(IHttpTransport http, ISecretProvider secrets, Uri? baseUri = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _baseUri = baseUri ?? DefaultBaseUri;
    }

    /// <inheritdoc />
    public string Name => "CurseForge";

    /// <inheritdoc />
    public async Task<Result<ResourceSearchResult>> SearchAsync(
        ResourceSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        string url = $"{_baseUri}mods/search?gameId={CurseForgeClassIds.MinecraftGameId}"
            + $"&searchFilter={Uri.EscapeDataString(query.Query)}"
            + $"&classId={CurseForgeClassIds.ForKind(query.Kind)}"
            + $"&index={query.Offset}&pageSize={query.Limit}"
            + "&sortOrder=desc";

        if (!string.IsNullOrEmpty(query.GameVersion))
        {
            url += $"&gameVersion={Uri.EscapeDataString(query.GameVersion)}";
        }

        if (query.Loader is { } loader && LoaderNames.ToCurseForge(loader) is { } loaderId)
        {
            url += $"&modLoaderType={loaderId.ToString(CultureInfo.InvariantCulture)}";
        }

        Result<string> body = await GetStringAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        if (!body.IsSuccess)
        {
            return Result<ResourceSearchResult>.Failure(body.Code, body.Detail);
        }

        using JsonDocument document = JsonDocument.Parse(body.Value!);
        List<ResourceProject> projects = [];
        foreach (JsonElement item in EnumerateData(document.RootElement))
        {
            projects.Add(ParseProject(item));
        }

        int total = projects.Count;
        int offset = query.Offset;
        int limit = query.Limit;
        if (document.RootElement.TryGetProperty("pagination", out JsonElement pagination))
        {
            total = GetInt(pagination, "totalCount") ?? total;
            offset = GetInt(pagination, "index") ?? offset;
            limit = GetInt(pagination, "pageSize") ?? limit;
        }

        return Result<ResourceSearchResult>.Success(new ResourceSearchResult(total, offset, limit, projects));
    }

    /// <inheritdoc />
    public async Task<Result<ResourceProject>> GetProjectAsync(string idOrSlug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrSlug);

        Result<string> body = await GetStringAsync(
            new Uri(_baseUri, "mods/" + Uri.EscapeDataString(idOrSlug)), cancellationToken).ConfigureAwait(false);
        if (!body.IsSuccess)
        {
            return Result<ResourceProject>.Failure(body.Code, body.Detail);
        }

        using JsonDocument document = JsonDocument.Parse(body.Value!);
        if (!document.RootElement.TryGetProperty("data", out JsonElement data))
        {
            return Result<ResourceProject>.Failure(ErrorCode.VersionParseFailed, "缺少 data。");
        }

        return Result<ResourceProject>.Success(ParseProject(data));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ResourceFile>>> GetFilesAsync(
        string idOrSlug,
        string? gameVersion = null,
        ModLoaderKind? loader = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrSlug);

        string url = $"{_baseUri}mods/{Uri.EscapeDataString(idOrSlug)}/files?pageSize=50";
        if (!string.IsNullOrEmpty(gameVersion))
        {
            url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
        }

        if (loader is { } kind && LoaderNames.ToCurseForge(kind) is { } loaderId)
        {
            url += $"&modLoaderType={loaderId.ToString(CultureInfo.InvariantCulture)}";
        }

        Result<string> body = await GetStringAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        if (!body.IsSuccess)
        {
            return Result<IReadOnlyList<ResourceFile>>.Failure(body.Code, body.Detail);
        }

        using JsonDocument document = JsonDocument.Parse(body.Value!);
        List<ResourceFile> files = [];
        foreach (JsonElement item in EnumerateData(document.RootElement))
        {
            if (ParseFile(item) is { } file)
            {
                files.Add(file);
            }
        }

        return Result<IReadOnlyList<ResourceFile>>.Success(files);
    }

    /// <summary>按文件 ID 批量获取文件（用于解析整合包中缺失的直链）。</summary>
    public async Task<Result<IReadOnlyList<ResourceFile>>> GetFilesByIdsAsync(
        IReadOnlyList<int> fileIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileIds);
        if (fileIds.Count == 0)
        {
            return Result<IReadOnlyList<ResourceFile>>.Success([]);
        }

        string body = System.Text.Json.JsonSerializer.Serialize(new { fileIds });
        string? apiKey = _secrets.GetSecret(ApiKeySecretName);
        if (string.IsNullOrEmpty(apiKey))
        {
            return Result<IReadOnlyList<ResourceFile>>.Failure(ErrorCode.ConfigInvalid, "缺少 CurseForge API Key。");
        }

        Dictionary<string, string> headers = new(StringComparer.Ordinal)
        {
            ["x-api-key"] = apiKey,
            ["Accept"] = "application/json",
        };

        Result<HttpResponse> sent = await _http
            .SendAsync(new HttpRequest(new Uri(_baseUri, "mods/files"), HttpVerb.Post, headers, null, HttpContentSpec.Json(body)), cancellationToken)
            .ConfigureAwait(false);
        if (!sent.IsSuccess)
        {
            return Result<IReadOnlyList<ResourceFile>>.Failure(sent.Code, sent.Detail);
        }

        using HttpResponse response = sent.Value!;
        if (response.StatusCode == 429)
        {
            return Result<IReadOnlyList<ResourceFile>>.Failure(ErrorCode.RateLimited, "CurseForge 限流。");
        }

        if (response.StatusCode == 403)
        {
            return Result<IReadOnlyList<ResourceFile>>.Failure(ErrorCode.AuthDenied, "CurseForge 拒绝访问。");
        }

        if (!response.IsSuccess)
        {
            return Result<IReadOnlyList<ResourceFile>>.Failure(ErrorCode.Network, $"CurseForge HTTP {response.StatusCode}");
        }

        using JsonDocument document = JsonDocument.Parse(await response.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        List<ResourceFile> files = [];
        foreach (JsonElement item in EnumerateData(document.RootElement))
        {
            if (ParseFile(item) is { } file)
            {
                files.Add(file);
            }
        }

        return Result<IReadOnlyList<ResourceFile>>.Success(files);
    }

    private static ResourceProject ParseProject(JsonElement element)
    {
        JsonElement primary = TryFirstFile(element);

        List<string> gameVersions = primary.ValueKind == JsonValueKind.Object
            ? ReadStringArray(primary, "gameVersions")
            : [];
        List<ModLoaderKind> loaders = [];
        if (primary.ValueKind == JsonValueKind.Object && LoaderNames.FromCurseForge(GetInt(primary, "modLoaderType")) is { } loader
            && loader != ModLoaderKind.None)
        {
            loaders.Add(loader);
        }

        return new ResourceProject(
            GetLong(element, "id")?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            GetString(element, "slug") ?? string.Empty,
            GetString(element, "name") ?? string.Empty,
            GetString(element, "summary"),
            ReadThumbnail(element),
            GetLong(element, "downloadCount") ?? 0,
            ReadFirstAuthor(element),
            CurseForgeClassIds.KindFor(GetInt(element, "classId") ?? 6),
            gameVersions,
            loaders);
    }

    private static ResourceFile? ParseFile(JsonElement element)
    {
        string? fileName = GetString(element, "fileName");
        long? fileId = GetLong(element, "id");
        if (fileName is null)
        {
            return null;
        }

        string? url = GetString(element, "downloadUrl");
        if (url is null && fileId is { } id)
        {
            // CurseForge 对部分文件不返回直链，回退到 forgecdn 约定路径。
            url = $"{ForgeCdnBase}{id / 1000}/{id % 1000}/{Uri.EscapeDataString(fileName)}";
        }

        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        string? sha1 = null;
        string? sha512 = null;
        if (element.TryGetProperty("hashes", out JsonElement hashes) && hashes.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement hash in hashes.EnumerateArray())
            {
                string? algo = GetInt(hash, "algo")?.ToString(CultureInfo.InvariantCulture);
                string? value = GetString(hash, "value");
                if (algo == "1")
                {
                    sha1 = value;
                }
                else if (algo == "2")
                {
                    sha512 = value;
                }
            }
        }

        List<ModLoaderKind> loaders = [];
        if (LoaderNames.FromCurseForge(GetInt(element, "modLoaderType")) is { } loader && loader != ModLoaderKind.None)
        {
            loaders.Add(loader);
        }

        return new ResourceFile(
            fileName,
            uri,
            sha1,
            sha512,
            GetLong(element, "fileLength") ?? 0,
            ReadStringArray(element, "gameVersions"),
            loaders,
            null,
            fileId?.ToString(CultureInfo.InvariantCulture));
    }

    private async Task<Result<string>> GetStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        string? apiKey = _secrets.GetSecret(ApiKeySecretName);
        if (string.IsNullOrEmpty(apiKey))
        {
            return Result<string>.Failure(ErrorCode.ConfigInvalid, "缺少 CurseForge API Key（" + ApiKeySecretName + "）。");
        }

        Dictionary<string, string> headers = new(StringComparer.Ordinal)
        {
            ["x-api-key"] = apiKey,
            ["Accept"] = "application/json",
        };

        Result<HttpResponse> sent = await _http.SendAsync(new HttpRequest(uri, HttpVerb.Get, headers), cancellationToken)
            .ConfigureAwait(false);
        if (!sent.IsSuccess)
        {
            return Result<string>.Failure(sent.Code, sent.Detail);
        }

        using HttpResponse response = sent.Value!;
        switch (response.StatusCode)
        {
            case 429:
                return Result<string>.Failure(ErrorCode.RateLimited, "CurseForge 限流。");
            case 403:
                return Result<string>.Failure(ErrorCode.AuthDenied, "CurseForge 拒绝访问（Key 无效或权限不足）。");
            case 404:
                return Result<string>.Failure(ErrorCode.NotFound, "CurseForge 未找到资源。");
        }

        if (!response.IsSuccess)
        {
            return Result<string>.Failure(ErrorCode.Network, $"CurseForge HTTP {response.StatusCode}");
        }

        return Result<string>.Success(await response.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
    }

    private static IEnumerable<JsonElement> EnumerateData(JsonElement root)
        => root.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray()
            : [];

    private static JsonElement TryFirstFile(JsonElement element)
        => element.TryGetProperty("latestFiles", out JsonElement files)
            && files.ValueKind == JsonValueKind.Array
            && files.GetArrayLength() > 0
            ? files[0]
            : default;

    private static string? ReadThumbnail(JsonElement element)
        => element.TryGetProperty("logo", out JsonElement logo) && logo.ValueKind == JsonValueKind.Object
            ? GetString(logo, "thumbnailUrl")
            : null;

    private static string? ReadFirstAuthor(JsonElement element)
        => element.TryGetProperty("authors", out JsonElement authors)
            && authors.ValueKind == JsonValueKind.Array
            && authors.GetArrayLength() > 0
            ? GetString(authors[0], "name")
            : null;

    private static List<string> ReadStringArray(JsonElement element, string property)
    {
        List<string> values = [];
        if (element.TryGetProperty(property, out JsonElement array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    values.Add(item.GetString()!);
                }
            }
        }

        return values;
    }

    private static string? GetString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    private static long? GetLong(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : null;
}