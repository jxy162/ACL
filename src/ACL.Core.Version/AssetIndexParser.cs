using System.Text.Json;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>资源索引（assets index）JSON 解析。</summary>
public static class AssetIndexParser
{
    /// <summary>解析资源索引。</summary>
    /// <param name="json">索引 JSON。</param>
    /// <param name="id">索引 ID（JSON 内不含，需外部提供）。</param>
    public static Result<AssetIndex> Parse(string json, string id)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<AssetIndex>.Failure(ErrorCode.ManifestCorrupt, "资源索引为空。");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return Result<AssetIndex>.Failure(ErrorCode.VersionParseFailed, ex.Message);
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("objects", out JsonElement objects) || objects.ValueKind != JsonValueKind.Object)
            {
                return Result<AssetIndex>.Failure(ErrorCode.ManifestCorrupt, "缺少 objects。");
            }

            Dictionary<string, AssetObject> map = new(StringComparer.Ordinal);
            foreach (JsonProperty property in objects.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                string? hash = GetString(property.Value, "hash");
                long? size = GetLong(property.Value, "size");
                if (hash is not null && size is not null)
                {
                    map[property.Name] = new AssetObject(hash, size.Value);
                }
            }

            return Result<AssetIndex>.Success(new AssetIndex(id, map));
        }
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? GetLong(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : null;
}