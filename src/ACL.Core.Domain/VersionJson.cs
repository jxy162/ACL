using System.Text.Json.Serialization;

namespace ACL.Core.Domain;

/// <summary>单个可下载产物（客户端 / 服务端 / 库文件）。</summary>
/// <param name="Url">下载地址（可空）。</param>
/// <param name="Sha1">SHA-1（可空）。</param>
/// <param name="Size">字节数（可空）。</param>
/// <param name="Path">库内的相对路径（可空）。</param>
public sealed record DownloadArtifact(Uri? Url, string? Sha1 = null, long? Size = null, string? Path = null);

/// <summary>库的下载信息。</summary>
/// <param name="Artifact">主产物。</param>
/// <param name="Classifiers">附加分类产物（如 natives）。</param>
public sealed record LibraryDownloads(
    DownloadArtifact? Artifact,
    IReadOnlyDictionary<string, DownloadArtifact>? Classifiers = null);

/// <summary>OS 规则条目。</summary>
/// <param name="Action">动作：allow / disallow。</param>
/// <param name="OsName">操作系统名（windows / linux / osx）。</param>
/// <param name="OsVersion">操作系统版本正则。</param>
/// <param name="OsArch">架构（x86 / x86_64 / arm64）。</param>
public sealed record RuleEntry(string Action, string? OsName = null, string? OsVersion = null, string? OsArch = null)
{
    /// <summary>是否允许。</summary>
    [JsonIgnore]
    public bool IsAllow => Action.Equals("allow", StringComparison.OrdinalIgnoreCase);
}

/// <summary>库引用。</summary>
/// <param name="Name">Maven 坐标（group:artifact:version）。</param>
/// <param name="Downloads">下载信息。</param>
/// <param name="Url">Maven 仓库根（可空）。</param>
/// <param name="Natives">natives 分类映射（OS → classifier）。</param>
/// <param name="Rules">OS 规则。</param>
public sealed record LibraryRef(
    string Name,
    LibraryDownloads? Downloads = null,
    string? Url = null,
    IReadOnlyDictionary<string, string>? Natives = null,
    IReadOnlyList<RuleEntry>? Rules = null);

/// <summary>资源索引引用。</summary>
/// <param name="Id">索引 ID（如 <c>5</c>）。</param>
/// <param name="Url">索引地址。</param>
/// <param name="Sha1">SHA-1（可空）。</param>
/// <param name="Size">字节数（可空）。</param>
/// <param name="TotalSize">资源总量（可空）。</param>
public sealed record AssetIndexRef(string Id, Uri Url, string? Sha1 = null, long? Size = null, long? TotalSize = null);

/// <summary>Java 版本需求。</summary>
/// <param name="Component">组件名（如 java-runtime-gamma）。</param>
/// <param name="MajorVersion">主版本号。</param>
public sealed record JavaVersionRef(string Component, int MajorVersion);

/// <summary>客户端 / 服务端下载信息。</summary>
/// <param name="Client">客户端产物。</param>
/// <param name="Server">服务端产物。</param>
public sealed record VersionDownloads(DownloadArtifact? Client = null, DownloadArtifact? Server = null);

/// <summary>一条启动参数（可带规则）。</summary>
/// <param name="Values">参数值列表（可能含占位符）。</param>
/// <param name="Rules">生效规则（可空表示无条件）。</param>
public sealed record ArgumentEntry(IReadOnlyList<string> Values, IReadOnlyList<RuleEntry>? Rules = null);

/// <summary>版本 JSON（可含 <see cref="InheritsFrom"/>）。</summary>
/// <param name="Id">版本名。</param>
/// <param name="InheritsFrom">继承的父版本名（可空）。</param>
/// <param name="MainClass">主类。</param>
/// <param name="Assets">资源版本。</param>
/// <param name="Type">类型。</param>
/// <param name="AssetIndex">资源索引。</param>
/// <param name="JavaVersion">Java 需求。</param>
/// <param name="Downloads">客户端 / 服务端产物。</param>
/// <param name="Libraries">库列表。</param>
/// <param name="GameArguments">游戏参数。</param>
/// <param name="JvmArguments">JVM 参数。</param>
/// <param name="LegacyMinecraftArguments">旧版单一参数字符串（可空）。</param>
public sealed record VersionJson(
    string Id,
    string? InheritsFrom,
    string? MainClass,
    string? Assets,
    string? Type,
    AssetIndexRef? AssetIndex,
    JavaVersionRef? JavaVersion,
    VersionDownloads? Downloads,
    IReadOnlyList<LibraryRef> Libraries,
    IReadOnlyList<ArgumentEntry> GameArguments,
    IReadOnlyList<ArgumentEntry> JvmArguments,
    string? LegacyMinecraftArguments);