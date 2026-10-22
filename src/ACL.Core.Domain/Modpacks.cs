namespace ACL.Core.Domain;

/// <summary>整合包来源格式。</summary>
public enum ModpackFormat
{
    /// <summary>CurseForge（<c>manifest.json</c>）。</summary>
    CurseForge,

    /// <summary>Modrinth（<c>.mrpack</c>，含 <c>modrinth.index.json</c>）。</summary>
    Modrinth,

    /// <summary>HMCL。</summary>
    Hmcl,

    /// <summary>MultiMC / Prism（<c>mmc-pack.json</c>）。</summary>
    Mmc,

    /// <summary>MCBBS。</summary>
    Mcbbs,

    /// <summary>通用压缩包（无清单，仅覆盖文件）。</summary>
    Generic,
}

/// <summary>整合包要求的加载器。</summary>
/// <param name="Kind">加载器种类。</param>
/// <param name="Version">版本。</param>
public sealed record ModpackLoader(ModLoaderKind Kind, string Version);

/// <summary>整合包中的一个文件。</summary>
/// <param name="RelativePath">相对游戏目录的路径。</param>
/// <param name="Downloads">候选直链（可能为空，需外部解析）。</param>
/// <param name="Sha1">SHA-1（可空）。</param>
/// <param name="Sha512">SHA-512（可空）。</param>
/// <param name="Size">字节数。</param>
/// <param name="Required">是否必需。</param>
/// <param name="ProjectId">来源项目 ID（如 CurseForge）。</param>
/// <param name="FileId">来源文件 ID（如 CurseForge）。</param>
public sealed record ModpackFile(
    string RelativePath,
    IReadOnlyList<Uri> Downloads,
    string? Sha1,
    string? Sha512,
    long Size,
    bool Required = true,
    int? ProjectId = null,
    int? FileId = null)
{
    /// <summary>是否已有可用直链。</summary>
    public bool IsResolved => Downloads.Count > 0;

    /// <summary>首选校验规格（优先 SHA-512，其次 SHA-1）。</summary>
    public HashSpec? PreferredHash
        => Sha512 is not null ? HashSpec.Sha512(Sha512)
        : Sha1 is not null ? HashSpec.Sha1(Sha1)
        : null;
}

/// <summary>整合包清单。</summary>
/// <param name="Name">名称。</param>
/// <param name="Version">版本（可空）。</param>
/// <param name="MinecraftVersion">目标 Minecraft 版本。</param>
/// <param name="Loaders">加载器要求。</param>
/// <param name="Files">文件列表。</param>
/// <param name="OverridesPrefix">压缩包内覆盖文件的前缀（如 <c>overrides/</c>）。</param>
/// <param name="Format">来源格式。</param>
public sealed record ModpackManifest(
    string Name,
    string? Version,
    string MinecraftVersion,
    IReadOnlyList<ModpackLoader> Loaders,
    IReadOnlyList<ModpackFile> Files,
    string OverridesPrefix,
    ModpackFormat Format)
{
    /// <summary>缺少直链、需要外部解析的文件。</summary>
    public IReadOnlyList<ModpackFile> UnresolvedFiles => [.. Files.Where(file => !file.IsResolved)];
}

/// <summary>整合包加载器标识解析（如 CurseForge 的 <c>forge-47.2.0</c>）。</summary>
public static class ModpackLoaderIds
{
    private static readonly string[] KnownNames =
    [
        "fabric-loader",
        "quilt-loader",
        "liteloader",
        "neoforge",
        "forge",
        "fabric",
        "quilt",
        "optifine",
    ];

    /// <summary>解析形如 <c>forge-47.2.0</c> / <c>fabric-loader-0.15.0</c> 的加载器标识。</summary>
    public static ModpackLoader? Parse(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (string name in KnownNames)
        {
            if (id.Length > name.Length + 1 && id.StartsWith(name + "-", StringComparison.OrdinalIgnoreCase))
            {
                ModLoaderKind kind = FromName(name);
                return kind == ModLoaderKind.None ? null : new ModpackLoader(kind, id[(name.Length + 1)..]);
            }
        }

        return null;
    }

    private static ModLoaderKind FromName(string name) => name switch
    {
        "forge" => ModLoaderKind.Forge,
        "neoforge" => ModLoaderKind.NeoForge,
        "fabric" or "fabric-loader" => ModLoaderKind.Fabric,
        "quilt" or "quilt-loader" => ModLoaderKind.Quilt,
        "liteloader" => ModLoaderKind.LiteLoader,
        "optifine" => ModLoaderKind.OptiFine,
        _ => ModLoaderKind.None,
    };
}