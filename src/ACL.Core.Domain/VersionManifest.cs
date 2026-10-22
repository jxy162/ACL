namespace ACL.Core.Domain;

/// <summary>清单中的"最新版本"信息。</summary>
/// <param name="Release">最新正式版。</param>
/// <param name="Snapshot">最新快照。</param>
public sealed record ManifestLatest(string Release, string Snapshot);

/// <summary>版本清单中的一个版本条目。</summary>
/// <param name="Id">版本名。</param>
/// <param name="Type">类型（release / snapshot / old_beta / old_alpha）。</param>
/// <param name="Url">该版本 JSON 的地址。</param>
/// <param name="ReleaseTime">发布时间（UTC）。</param>
/// <param name="Time">元数据更新时间（UTC）。</param>
/// <param name="Sha1">版本 JSON 的 SHA-1（可空）。</param>
/// <param name="ComplianceLevel">合规级别。</param>
public sealed record VersionEntry(
    string Id,
    string Type,
    Uri Url,
    DateTimeOffset ReleaseTime,
    DateTimeOffset Time,
    string? Sha1,
    int ComplianceLevel);

/// <summary>版本清单。</summary>
/// <param name="Latest">最新版本信息。</param>
/// <param name="Versions">全部版本条目（通常按新到旧）。</param>
public sealed record VersionManifest(ManifestLatest Latest, IReadOnlyList<VersionEntry> Versions);