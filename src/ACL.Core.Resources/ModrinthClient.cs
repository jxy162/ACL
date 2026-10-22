using System.Text.Json;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Resources;

/// <summary>Modrinth API v2 客户端（免鉴权）。</summary>
public sealed class ModrinthClient : IResourceIndex
{
    /// <summary>官方基址。</summary>
    public static readonly Uri DefaultBaseUri = new("https://api.modrinth.com/v2/");

    private readonly IHttpTransport _http;
    private readonly Uri _baseUri;

    /// <summary>构造客户端。</summary>
    public ModrinthClient(IHttpTransport http, Uri? baseUri = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _baseUri = baseUri ?? DefaultBaseUri;
    }

    /// <inheritdoc />
    public string Name => "Modrinth";

    /// <inheritdoc />
    public async Task<Result<ResourceSearchResult>> SearchAsync(
        ResourceSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        List<string> facetGroups = [$"[\"project_type:{ProjectType(query.Kind)}\"]"];
        if (!string.IsNullOrEmpty(query.GameVersion))
        {
            facetGroups.Add($"[\"versions:{query.GameVersion}\"]");
        }

        if (query.Loader is { } loader && LoaderNames.ToModrinth(loader) is { } loaderName)
        {
            facetGroups.Add($"[\"categories:{loaderName}\"]");
        }

        string url = $"{_baseUri}search?query={Uri.EscapeDataString(query.Query)}"
            + $"&facets={Uri.EscapeDataString("[" + string.Join(",", facetGroups) + "]")}"
            + $"&offset={query.Offset}&limit={query.Limit}&index={SortName(query.Sort)}";

        Result<string> body = await GetStringAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        if (!body.IsSuccess)
        {
            return Result<ResourceSearchResult>.Failure(body.Code, body.Detail);
        }

        return Result<ResourceSearchResult>.Success(ParseSearch(body.Value!));
    }

    /// <inheritdoc />
    public async Task<Result<ResourceProject>> GetProjectAsync(string idOrSlug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrSlug);

        Result<string> body = await GetStringAsync(new Uri(_baseUri, "project/" + Uri.EscapeDataString(idOrSlug)), cancellationToken)
            .ConfigureAwait(false);
        if (!body.IsSuccess)
        {
            return Result<ResourceProject>.Failure(body.Code, body.Detail);
        }

        using JsonDocument document = JsonDocument.Parse(body.Value!);
        return Result<ResourceProject>.Success(ParseProject(document.RootElement));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ResourceFile>>> GetFilesAsync(
        string idOrSlug,
        string? gameVersion = null,
        ModLoaderKind? loader = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrSlug);

        List<string> query = [];
        if (!string.IsNullOrEmpty(gameVersion))
        {
            query.Add($"game_versions={Uri.EscapeDataString("[\"" + gameVersion + "\"]")}");
        }

        if (loader is { } kind && LoaderNames.ToModrinth(kind) is { } loaderName)
        {
            query.Add($"loaders={Uri.EscapeDataString("[\"" + loaderName + "\"]")}");
        }

        string url = $"{_baseUri}project/{Uri.EscapeDataString(idOrSlug)}/version";
        if (query.Count > 0)
        {
            url += "?" + string.Join("&", query);
        }

        Result<string> body = await GetStringAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        if (!body.IsSuccess)
        {
            return Result<IReadOnlyList<ResourceFile>>.Failure(body.Code, body.Detail);
        }

        using JsonDocument document = JsonDocument.Parse(body.Value!);
        List<ResourceFile> files = [];
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement version in document.RootElement.EnumerateArray())
            {
                if (ParseFile(version) is { } file)
                {
                    files.Add(file);
                }
            }
        }

        return Result<IReadOnlyList<ResourceFile>>.Success(files);
    }

    private static ResourceSearchResult ParseSearch(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        List<ResourceProject> projects = [];
        if (root.TryGetProperty("hits", out JsonElement hits) && hits.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement hit in hits.EnumerateArray())
            {
                projects.Add(ParseProject(hit));
            }
        }

        return new ResourceSearchResult(
            GetInt(root, "total_hits") ?? projects.Count,
            GetInt(root, "offset") ?? 0,
            GetInt(root, "limit") ?? projects.Count,
            projects);
    }

    private static ResourceProject ParseProject(JsonElement element)
    {
        List<string> versions = ReadStringArray(element, "versions");
        List<ModLoaderKind> loaders = [];
        foreach (string category in ReadStringArray(element, "categories"))
        {
            ModLoaderKind kind = LoaderNames.FromModrinth(category);
            if (kind != ModLoaderKind.None)
            {
                loaders.Add(kind);
            }
        }

        return new ResourceProject(
            GetString(element, "project_id") ?? GetString(element, "id") ?? string.Empty,
            GetString(element, "slug") ?? string.Empty,
            GetString(element, "title") ?? string.Empty,
            GetString(element, "description"),
            GetString(element, "icon_url"),
            GetLong(element, "downloads") ?? 0,
            GetString(element, "author") ?? ReadFirstAuthor(element),
            ParseKind(GetString(element, "project_type")),
            versions,
            loaders);
    }

    private static ResourceFile? ParseFile(JsonElement version)
    {
        if (!version.TryGetProperty("files", out JsonElement files)
            || files.ValueKind != JsonValueKind.Array
            || files.GetArrayLength() == 0)
        {
            return null;
        }

        JsonElement primary = files[0];
        string? url = GetString(primary, "url");
        string? fileName = GetString(primary, "filename");
        if (url is null || fileName is null || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        string? sha1 = null;
        string? sha512 = null;
        if (primary.TryGetProperty("hashes", out JsonElement hashes) && hashes.ValueKind == JsonValueKind.Object)
        {
            sha1 = GetString(hashes, "sha1");
            sha512 = GetString(hashes, "sha512");
        }

        List<ModLoaderKind> loaders = [];
        foreach (string loaderName in ReadStringArray(version, "loaders"))
        {
            ModLoaderKind kind = LoaderNames.FromModrinth(loaderName);
            if (kind != ModLoaderKind.None)
            {
                loaders.Add(kind);
            }
        }

        return new ResourceFile(
            fileName,
            uri,
            sha1,
            sha512,
            GetLong(primary, "size") ?? 0,
            ReadStringArray(version, "game_versions"),
            loaders,
            GetString(version, "version_type"));
    }

    private async Task<Result<string>> GetStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        Result<HttpResponse> sent = await _http.SendAsync(new HttpRequest(uri), cancellationToken).ConfigureAwait(false);
        if (!sent.IsSuccess)
        {
            return Result<string>.Failure(sent.Code, sent.Detail);
        }

        using HttpResponse response = sent.Value!;
        if (response.StatusCode == 429)
        {
            return Result<string>.Failure(ErrorCode.RateLimited, "Modrinth 限流。");
        }

        if (!response.IsSuccess)
        {
            return Result<string>.Failure(
                response.StatusCode == 404 ? ErrorCode.NotFound : ErrorCode.Network,
                $"Modrinth HTTP {response.StatusCode}");
        }

        return Result<string>.Success(await response.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
    }

    private static string ProjectType(ResourceKind kind) => kind switch
    {
        ResourceKind.Modpack => "modpack",
        ResourceKind.ResourcePack => "resourcepack",
        ResourceKind.ShaderPack => "shader",
        ResourceKind.DataPack => "datapack",
        ResourceKind.World => "world",
        _ => "mod",
    };

    private static ResourceKind ParseKind(string? projectType) => projectType switch
    {
        "modpack" => ResourceKind.Modpack,
        "resourcepack" => ResourceKind.ResourcePack,
        "shader" => ResourceKind.ShaderPack,
        "datapack" => ResourceKind.DataPack,
        "world" => ResourceKind.World,
        _ => ResourceKind.Mod,
    };

    private static string SortName(ResourceSortField sort) => sort switch
    {
        ResourceSortField.Downloads => "downloads",
        ResourceSortField.Updated => "updated",
        ResourceSortField.Newest => "newest",
        ResourceSortField.Follows => "follows",
        _ => "relevance",
    };

    private static string? ReadFirstAuthor(JsonElement element)
        => element.TryGetProperty("author", out JsonElement author) && author.ValueKind == JsonValueKind.String
            ? author.GetString()
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
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    private static long? GetLong(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : null;
}