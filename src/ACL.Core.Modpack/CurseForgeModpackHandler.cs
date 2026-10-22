using System.Text.Json;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Modpack;

/// <summary>CurseForge 整合包（<c>manifest.json</c>）处理。文件需经外部解析（projectID/fileID）。</summary>
public sealed class CurseForgeModpackHandler : IModpackFormatHandler
{
    /// <inheritdoc />
    public string Name => "CurseForge";

    /// <inheritdoc />
    public bool CanHandle(IModpackArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        return archive.Contains("manifest.json");
    }

    /// <inheritdoc />
    public Result<ModpackManifest> Parse(IModpackArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        string? json = archive.ReadText("manifest.json");
        if (json is null)
        {
            return Result<ModpackManifest>.Failure(ErrorCode.ManifestCorrupt, "缺少 manifest.json。");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            JsonElement minecraft = root.TryGetProperty("minecraft", out JsonElement mc) && mc.ValueKind == JsonValueKind.Object
                ? mc
                : default;

            string minecraftVersion = minecraft.ValueKind == JsonValueKind.Object ? GetString(minecraft, "version") ?? string.Empty : string.Empty;

            List<ModpackLoader> loaders = [];
            if (minecraft.ValueKind == JsonValueKind.Object
                && minecraft.TryGetProperty("modLoaders", out JsonElement modLoaders)
                && modLoaders.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in modLoaders.EnumerateArray())
                {
                    if (GetString(item, "id") is { } id && ModpackLoaderIds.Parse(id) is { } loader)
                    {
                        loaders.Add(loader);
                    }
                }
            }

            List<ModpackFile> files = [];
            if (root.TryGetProperty("files", out JsonElement fileArray) && fileArray.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in fileArray.EnumerateArray())
                {
                    int? projectId = GetInt(item, "projectID");
                    int? fileId = GetInt(item, "fileID");
                    if (projectId is null || fileId is null)
                    {
                        continue;
                    }

                    bool required = !item.TryGetProperty("required", out JsonElement requiredElement)
                        || requiredElement.ValueKind != JsonValueKind.False;

                    files.Add(new ModpackFile(
                        string.Empty,
                        [],
                        null,
                        null,
                        0,
                        required,
                        projectId,
                        fileId));
                }
            }

            string overrides = GetString(root, "overrides") ?? "overrides";
            return Result<ModpackManifest>.Success(new ModpackManifest(
                GetString(root, "name") ?? "CurseForge Pack",
                GetString(root, "version"),
                minecraftVersion,
                loaders,
                files,
                overrides,
                ModpackFormat.CurseForge));
        }
        catch (JsonException ex)
        {
            return Result<ModpackManifest>.Failure(ErrorCode.VersionParseFailed, ex.Message);
        }
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
}