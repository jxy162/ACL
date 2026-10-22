using System.Text.Json;
using System.Text.Json.Serialization;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>版本清单 JSON 解析（官方 Mojang 格式）。</summary>
public static class VersionManifestParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>解析清单 JSON。</summary>
    public static Result<VersionManifest> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<VersionManifest>.Failure(ErrorCode.ManifestCorrupt, "清单为空。");
        }

        ManifestDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ManifestDocument>(json, Options);
        }
        catch (JsonException ex)
        {
            return Result<VersionManifest>.Failure(ErrorCode.VersionParseFailed, ex.Message);
        }

        if (document?.Versions is null || document.Latest is null)
        {
            return Result<VersionManifest>.Failure(ErrorCode.ManifestCorrupt, "缺少 latest 或 versions。");
        }

        List<VersionEntry> entries = [];
        foreach (VersionDocument item in document.Versions)
        {
            if (string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Url))
            {
                continue;
            }

            if (!Uri.TryCreate(item.Url, UriKind.Absolute, out Uri? url))
            {
                continue;
            }

            entries.Add(new VersionEntry(
                item.Id,
                item.Type ?? "unknown",
                url,
                item.ReleaseTime ?? default,
                item.Time ?? default,
                item.Sha1,
                item.ComplianceLevel ?? 0));
        }

        ManifestLatest latest = new(document.Latest.Release ?? string.Empty, document.Latest.Snapshot ?? string.Empty);
        return Result<VersionManifest>.Success(new VersionManifest(latest, entries));
    }

    private sealed class ManifestDocument
    {
        [JsonPropertyName("latest")]
        public LatestDocument? Latest { get; set; }

        [JsonPropertyName("versions")]
        public List<VersionDocument>? Versions { get; set; }
    }

    private sealed class LatestDocument
    {
        [JsonPropertyName("release")]
        public string? Release { get; set; }

        [JsonPropertyName("snapshot")]
        public string? Snapshot { get; set; }
    }

    private sealed class VersionDocument
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("releaseTime")]
        public DateTimeOffset? ReleaseTime { get; set; }

        [JsonPropertyName("time")]
        public DateTimeOffset? Time { get; set; }

        [JsonPropertyName("sha1")]
        public string? Sha1 { get; set; }

        [JsonPropertyName("complianceLevel")]
        public int? ComplianceLevel { get; set; }
    }
}