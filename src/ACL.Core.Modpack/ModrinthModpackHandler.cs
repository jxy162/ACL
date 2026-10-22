using System.Text.Json;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Modpack;

/// <summary>Modrinth 整合包（<c>modrinth.index.json</c>）处理。</summary>
public sealed class ModrinthModpackHandler : IModpackFormatHandler
{
    /// <inheritdoc />
    public string Name => "Modrinth";

    /// <inheritdoc />
    public bool CanHandle(IModpackArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        return archive.Contains("modrinth.index.json");
    }

    /// <inheritdoc />
    public Result<ModpackManifest> Parse(IModpackArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        string? json = archive.ReadText("modrinth.index.json");
        if (json is null)
        {
            return Result<ModpackManifest>.Failure(ErrorCode.ManifestCorrupt, "缺少 modrinth.index.json。");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            string name = GetString(root, "name") ?? "Modrinth Pack";
            string minecraft = ReadDependency(root, "minecraft") ?? string.Empty;
            List<ModpackLoader> loaders = [];
            foreach (string key in new[] { "forge", "neoforge", "fabric-loader", "quilt-loader" })
            {
                if (ReadDependency(root, key) is { } loaderVersion
                    && ModpackLoaderIds.Parse(key + "-" + loaderVersion) is { } loader)
                {
                    loaders.Add(loader);
                }
            }

            List<ModpackFile> files = [];
            if (root.TryGetProperty("files", out JsonElement fileArray) && fileArray.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in fileArray.EnumerateArray())
                {
                    ModpackFile? file = ParseFile(item);
                    if (file is not null)
                    {
                        files.Add(file);
                    }
                }
            }

            return Result<ModpackManifest>.Success(new ModpackManifest(
                name,
                GetString(root, "versionId"),
                minecraft,
                loaders,
                files,
                "overrides",
                ModpackFormat.Modrinth));
        }
        catch (JsonException ex)
        {
            return Result<ModpackManifest>.Failure(ErrorCode.VersionParseFailed, ex.Message);
        }
    }

    private static ModpackFile? ParseFile(JsonElement item)
    {
        string? path = GetString(item, "path");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        bool required = true;
        if (item.TryGetProperty("env", out JsonElement env) && env.ValueKind == JsonValueKind.Object
            && GetString(env, "client") is { } client)
        {
            required = !client.Equals("unsupported", StringComparison.OrdinalIgnoreCase);
        }

        List<Uri> downloads = [];
        if (item.TryGetProperty("downloads", out JsonElement urls) && urls.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement url in urls.EnumerateArray())
            {
                if (url.ValueKind == JsonValueKind.String && Uri.TryCreate(url.GetString(), UriKind.Absolute, out Uri? uri))
                {
                    downloads.Add(uri);
                }
            }
        }

        string? sha1 = null;
        string? sha512 = null;
        if (item.TryGetProperty("hashes", out JsonElement hashes) && hashes.ValueKind == JsonValueKind.Object)
        {
            sha1 = GetString(hashes, "sha1");
            sha512 = GetString(hashes, "sha512");
        }

        return new ModpackFile(path, downloads, sha1, sha512, GetLong(item, "fileSize") ?? 0, required);
    }

    private static string? ReadDependency(JsonElement root, string key)
        => root.TryGetProperty("dependencies", out JsonElement dependencies) && dependencies.ValueKind == JsonValueKind.Object
            ? GetString(dependencies, key)
            : null;

    private static string? GetString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? GetLong(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : null;
}