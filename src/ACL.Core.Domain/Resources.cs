namespace ACL.Core.Domain;

/// <summary>社区资源种类。</summary>
public enum ResourceKind
{
    /// <summary>模组。</summary>
    Mod,

    /// <summary>资源包。</summary>
    ResourcePack,

    /// <summary>光影包。</summary>
    ShaderPack,

    /// <summary>数据包。</summary>
    DataPack,

    /// <summary>地图 / 存档。</summary>
    World,

    /// <summary>整合包。</summary>
    Modpack,
}

/// <summary>检索排序字段。</summary>
public enum ResourceSortField
{
    /// <summary>相关度。</summary>
    Relevance,

    /// <summary>下载量。</summary>
    Downloads,

    /// <summary>最近更新。</summary>
    Updated,

    /// <summary>最新发布。</summary>
    Newest,

    /// <summary>关注数。</summary>
    Follows,
}

/// <summary>资源项目摘要。</summary>
/// <param name="Id">项目 ID。</param>
/// <param name="Slug">短名。</param>
/// <param name="Title">标题。</param>
/// <param name="Description">简介。</param>
/// <param name="IconUrl">图标地址。</param>
/// <param name="Downloads">下载量。</param>
/// <param name="Author">作者。</param>
/// <param name="Kind">种类。</param>
/// <param name="GameVersions">支持的游戏版本。</param>
/// <param name="Loaders">支持的加载器。</param>
public sealed record ResourceProject(
    string Id,
    string Slug,
    string Title,
    string? Description,
    string? IconUrl,
    long Downloads,
    string? Author,
    ResourceKind Kind,
    IReadOnlyList<string> GameVersions,
    IReadOnlyList<ModLoaderKind> Loaders);

/// <summary>资源文件。</summary>
/// <param name="FileName">文件名。</param>
/// <param name="Url">下载地址。</param>
/// <param name="Sha1">SHA-1（可空）。</param>
/// <param name="Sha512">SHA-512（可空）。</param>
/// <param name="Size">字节数。</param>
/// <param name="GameVersions">支持的游戏版本。</param>
/// <param name="Loaders">支持的加载器。</param>
/// <param name="ReleaseType">发布类型（release / beta / alpha）。</param>
/// <param name="SourceId">来源站的文件 ID（如 CurseForge fileId），用于精确回映射。</param>
public sealed record ResourceFile(
    string FileName,
    Uri Url,
    string? Sha1,
    string? Sha512,
    long Size,
    IReadOnlyList<string> GameVersions,
    IReadOnlyList<ModLoaderKind> Loaders,
    string? ReleaseType,
    string? SourceId = null)
{
    /// <summary>首选校验规格（优先 SHA-512，其次 SHA-1）。</summary>
    public HashSpec? PreferredHash
        => Sha512 is not null ? HashSpec.Sha512(Sha512)
        : Sha1 is not null ? HashSpec.Sha1(Sha1)
        : null;
}

/// <summary>检索结果分页。</summary>
/// <param name="Total">总条数。</param>
/// <param name="Offset">偏移。</param>
/// <param name="Limit">每页条数。</param>
/// <param name="Projects">项目列表。</param>
public sealed record ResourceSearchResult(
    int Total,
    int Offset,
    int Limit,
    IReadOnlyList<ResourceProject> Projects);

/// <summary>加载器名称 / 编号的跨站映射（Modrinth 用字符串，CurseForge 用整数）。</summary>
public static class LoaderNames
{
    /// <summary>转换为 Modrinth 名称；不支持返回 <see langword="null"/>。</summary>
    public static string? ToModrinth(ModLoaderKind kind) => kind switch
    {
        ModLoaderKind.Forge => "forge",
        ModLoaderKind.NeoForge => "neoforge",
        ModLoaderKind.Fabric => "fabric",
        ModLoaderKind.Quilt => "quilt",
        ModLoaderKind.LiteLoader => "liteloader",
        _ => null,
    };

    /// <summary>由 Modrinth 名称解析。</summary>
    public static ModLoaderKind FromModrinth(string? name) => name?.ToLowerInvariant() switch
    {
        "forge" => ModLoaderKind.Forge,
        "neoforge" => ModLoaderKind.NeoForge,
        "fabric" => ModLoaderKind.Fabric,
        "quilt" => ModLoaderKind.Quilt,
        "liteloader" => ModLoaderKind.LiteLoader,
        "optifine" => ModLoaderKind.OptiFine,
        _ => ModLoaderKind.None,
    };

    /// <summary>转换为 CurseForge 的 <c>ModLoaderType</c>；不支持返回 <see langword="null"/>。</summary>
    public static int? ToCurseForge(ModLoaderKind kind) => kind switch
    {
        ModLoaderKind.Forge => 1,
        ModLoaderKind.LiteLoader => 3,
        ModLoaderKind.Fabric => 4,
        ModLoaderKind.Quilt => 5,
        ModLoaderKind.NeoForge => 6,
        _ => null,
    };

    /// <summary>由 CurseForge 编号解析。</summary>
    public static ModLoaderKind FromCurseForge(int? value) => value switch
    {
        1 => ModLoaderKind.Forge,
        3 => ModLoaderKind.LiteLoader,
        4 => ModLoaderKind.Fabric,
        5 => ModLoaderKind.Quilt,
        6 => ModLoaderKind.NeoForge,
        _ => ModLoaderKind.None,
    };
}

/// <summary>CurseForge 分类编号。</summary>
public static class CurseForgeClassIds
{
    /// <summary>Minecraft 游戏 ID。</summary>
    public const int MinecraftGameId = 432;

    /// <summary>按资源种类取分类编号。</summary>
    public static int ForKind(ResourceKind kind) => kind switch
    {
        ResourceKind.Mod => 6,
        ResourceKind.ResourcePack => 12,
        ResourceKind.ShaderPack => 6552,
        ResourceKind.DataPack => 6945,
        ResourceKind.World => 17,
        ResourceKind.Modpack => 4471,
        _ => 6,
    };

    /// <summary>由分类编号反推资源种类。</summary>
    public static ResourceKind KindFor(int classId) => classId switch
    {
        12 => ResourceKind.ResourcePack,
        6552 => ResourceKind.ShaderPack,
        6945 => ResourceKind.DataPack,
        17 => ResourceKind.World,
        4471 => ResourceKind.Modpack,
        _ => ResourceKind.Mod,
    };
}