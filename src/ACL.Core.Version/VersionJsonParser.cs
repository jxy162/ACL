using System.Text.Json;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>版本 JSON 解析（官方 Mojang 格式，含 inheritsFrom / arguments / libraries）。</summary>
public static class VersionJsonParser
{
    /// <summary>解析版本 JSON。</summary>
    public static Result<VersionJson> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<VersionJson>.Failure(ErrorCode.ManifestCorrupt, "版本 JSON 为空。");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return Result<VersionJson>.Failure(ErrorCode.VersionParseFailed, ex.Message);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            string? id = GetString(root, "id");
            if (string.IsNullOrEmpty(id))
            {
                return Result<VersionJson>.Failure(ErrorCode.ManifestCorrupt, "缺少 id。");
            }

            AssetIndexRef? assetIndex = ParseAssetIndex(root);
            VersionDownloads? downloads = ParseDownloads(root);
            JavaVersionRef? javaVersion = ParseJavaVersion(root);
            List<LibraryRef> libraries = ParseLibraries(root);
            (List<ArgumentEntry> game, List<ArgumentEntry> jvm) = ParseArguments(root);

            VersionJson version = new(
                id,
                GetString(root, "inheritsFrom"),
                GetString(root, "mainClass"),
                GetString(root, "assets"),
                GetString(root, "type"),
                assetIndex,
                javaVersion,
                downloads,
                libraries,
                game,
                jvm,
                GetString(root, "minecraftArguments"));

            return Result<VersionJson>.Success(version);
        }
    }

    private static AssetIndexRef? ParseAssetIndex(JsonElement root)
    {
        if (!root.TryGetProperty("assetIndex", out JsonElement element) || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? id = GetString(element, "id");
        string? url = GetString(element, "url");
        if (string.IsNullOrEmpty(id) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        return new AssetIndexRef(id, uri, GetString(element, "sha1"), GetLong(element, "size"), GetLong(element, "totalSize"));
    }

    private static VersionDownloads? ParseDownloads(JsonElement root)
    {
        if (!root.TryGetProperty("downloads", out JsonElement element) || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new VersionDownloads(
            ParseArtifact(element, "client"),
            ParseArtifact(element, "server"));
    }

    private static JavaVersionRef? ParseJavaVersion(JsonElement root)
    {
        if (!root.TryGetProperty("javaVersion", out JsonElement element) || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? component = GetString(element, "component");
        int? major = GetInt(element, "majorVersion");
        return component is null || major is null ? null : new JavaVersionRef(component, major.Value);
    }

    private static List<LibraryRef> ParseLibraries(JsonElement root)
    {
        List<LibraryRef> libraries = [];
        if (!root.TryGetProperty("libraries", out JsonElement array) || array.ValueKind != JsonValueKind.Array)
        {
            return libraries;
        }

        foreach (JsonElement item in array.EnumerateArray())
        {
            string? name = GetString(item, "name");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            LibraryDownloads? downloads = null;
            if (item.TryGetProperty("downloads", out JsonElement dl) && dl.ValueKind == JsonValueKind.Object)
            {
                downloads = new LibraryDownloads(ParseArtifact(dl, "artifact"), ParseClassifiers(dl));
            }

            libraries.Add(new LibraryRef(name, downloads, GetString(item, "url"), GetNatives(item), ParseRules(item)));
        }

        return libraries;
    }

    private static Dictionary<string, DownloadArtifact>? ParseClassifiers(JsonElement downloads)
    {
        if (!downloads.TryGetProperty("classifiers", out JsonElement classifiers) || classifiers.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        Dictionary<string, DownloadArtifact> result = new(StringComparer.Ordinal);
        foreach (JsonProperty property in classifiers.EnumerateObject())
        {
            if (ParseArtifactValue(property.Value) is { } artifact)
            {
                result[property.Name] = artifact;
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static Dictionary<string, string>? GetNatives(JsonElement item)
    {
        if (!item.TryGetProperty("natives", out JsonElement natives) || natives.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in natives.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                result[property.Name] = property.Value.GetString()!.Replace("${arch}", "64", StringComparison.Ordinal);
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static List<RuleEntry>? ParseRules(JsonElement item)
    {
        if (!item.TryGetProperty("rules", out JsonElement rules) || rules.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<RuleEntry> result = [];
        foreach (JsonElement rule in rules.EnumerateArray())
        {
            string? action = GetString(rule, "action");
            if (string.IsNullOrEmpty(action))
            {
                continue;
            }

            string? osName = null;
            string? osVersion = null;
            string? osArch = null;
            if (rule.TryGetProperty("os", out JsonElement os) && os.ValueKind == JsonValueKind.Object)
            {
                osName = GetString(os, "name");
                osVersion = GetString(os, "version");
                osArch = GetString(os, "arch");
            }

            result.Add(new RuleEntry(action, osName, osVersion, osArch));
        }

        return result.Count == 0 ? null : result;
    }

    private static (List<ArgumentEntry> Game, List<ArgumentEntry> Jvm) ParseArguments(JsonElement root)
    {
        List<ArgumentEntry> game = [];
        List<ArgumentEntry> jvm = [];
        if (!root.TryGetProperty("arguments", out JsonElement arguments) || arguments.ValueKind != JsonValueKind.Object)
        {
            return (game, jvm);
        }

        if (arguments.TryGetProperty("game", out JsonElement gameElement))
        {
            game = ParseArgumentArray(gameElement);
        }

        if (arguments.TryGetProperty("jvm", out JsonElement jvmElement))
        {
            jvm = ParseArgumentArray(jvmElement);
        }

        return (game, jvm);
    }

    private static List<ArgumentEntry> ParseArgumentArray(JsonElement array)
    {
        List<ArgumentEntry> entries = [];
        if (array.ValueKind != JsonValueKind.Array)
        {
            return entries;
        }

        foreach (JsonElement item in array.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    entries.Add(new ArgumentEntry([item.GetString()!]));
                    break;

                case JsonValueKind.Object:
                    List<string> values = [];
                    if (item.TryGetProperty("value", out JsonElement value))
                    {
                        if (value.ValueKind == JsonValueKind.String)
                        {
                            values.Add(value.GetString()!);
                        }
                        else if (value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement v in value.EnumerateArray())
                            {
                                if (v.ValueKind == JsonValueKind.String)
                                {
                                    values.Add(v.GetString()!);
                                }
                            }
                        }
                    }

                    if (values.Count > 0)
                    {
                        entries.Add(new ArgumentEntry(values, ParseRules(item)));
                    }

                    break;

                default:
                    break;
            }
        }

        return entries;
    }

    private static DownloadArtifact? ParseArtifact(JsonElement parent, string property)
        => parent.TryGetProperty(property, out JsonElement element) ? ParseArtifactValue(element) : null;

    private static DownloadArtifact? ParseArtifactValue(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        Uri? url = Uri.TryCreate(GetString(element, "url"), UriKind.Absolute, out Uri? parsed) ? parsed : null;
        return new DownloadArtifact(url, GetString(element, "sha1"), GetLong(element, "size"), GetString(element, "path"));
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? GetLong(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : null;

    private static int? GetInt(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;
}