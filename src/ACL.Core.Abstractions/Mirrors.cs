namespace ACL.Core.Abstractions;

/// <summary>请求的资源种类，用于选择镜像替换规则与官方/镜像偏好。</summary>
public enum MirrorKind
{
    /// <summary>版本清单（version_manifest）。</summary>
    VersionManifest,

    /// <summary>单个版本 JSON / 元数据。</summary>
    VersionMeta,

    /// <summary>库文件（libraries）。</summary>
    Library,

    /// <summary>资源对象（assets objects）。</summary>
    Asset,

    /// <summary>资源索引（assets index）。</summary>
    AssetIndex,

    /// <summary>Java 运行时包。</summary>
    JavaRuntime,

    /// <summary>Forge 元数据。</summary>
    ForgeMeta,

    /// <summary>NeoForge 元数据。</summary>
    NeoForgeMeta,

    /// <summary>Fabric 元数据。</summary>
    FabricMeta,

    /// <summary>Quilt 元数据。</summary>
    QuiltMeta,

    /// <summary>LiteLoader 元数据。</summary>
    LiteLoaderMeta,

    /// <summary>OptiFine 元数据 / 安装包。</summary>
    OptiFineMeta,

    /// <summary>社区资源文件（CurseForge / Modrinth CDN）。</summary>
    ResourceFile,
}

/// <summary>镜像提供方：把原始 URL 重写为一个或多个镜像 URL（可插拔）。</summary>
public interface IMirrorProvider
{
    /// <summary>提供方名称。</summary>
    string Name { get; }

    /// <summary>尝试重写；不支持该种类 / 主机时返回空序列。</summary>
    IEnumerable<Uri> Rewrite(MirrorKind kind, Uri origin);
}

/// <summary>镜像策略：把原始 URL 解析为有序候选源，并决定官方 / 镜像偏好。</summary>
public interface IMirrorPolicy
{
    /// <summary>解析为有序候选（含官方与镜像）。</summary>
    IReadOnlyList<Uri> Resolve(MirrorKind kind, Uri origin);

    /// <summary>该种类是否优先官方源。</summary>
    bool PreferOfficial(MirrorKind kind);
}