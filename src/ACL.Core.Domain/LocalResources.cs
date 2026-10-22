namespace ACL.Core.Domain;

/// <summary>本地模组元数据的格式。</summary>
public enum ModMetadataFormat
{
    /// <summary>未识别。</summary>
    Unknown,

    /// <summary>Fabric（<c>fabric.mod.json</c>）。</summary>
    Fabric,

    /// <summary>Quilt（<c>quilt.mod.json</c>）。</summary>
    Quilt,

    /// <summary>Forge（<c>META-INF/mods.toml</c>）。</summary>
    ForgeToml,

    /// <summary>NeoForge（<c>META-INF/neoforge.mods.toml</c>）。</summary>
    NeoForgeToml,

    /// <summary>旧版 Forge（<c>mcmod.info</c>）。</summary>
    LegacyMcModInfo,
}

/// <summary>从 jar 中解析出的本地模组元数据。</summary>
/// <param name="FilePath">jar 路径。</param>
/// <param name="Format">元数据格式。</param>
/// <param name="ModId">模组 ID。</param>
/// <param name="Name">显示名。</param>
/// <param name="Version">版本。</param>
/// <param name="Authors">作者列表。</param>
/// <param name="Dependencies">依赖的模组 ID（尽力解析）。</param>
public sealed record LocalResourceInfo(
    string FilePath,
    ModMetadataFormat Format,
    string? ModId,
    string? Name,
    string? Version,
    IReadOnlyList<string> Authors,
    IReadOnlyList<string> Dependencies)
{
    /// <summary>是否识别到模组元数据。</summary>
    public bool IsRecognized => Format != ModMetadataFormat.Unknown;
}