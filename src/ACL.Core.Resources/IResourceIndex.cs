using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Resources;

/// <summary>资源检索查询。</summary>
/// <param name="Query">关键词。</param>
/// <param name="Kind">资源种类。</param>
/// <param name="GameVersion">限定的游戏版本（可空）。</param>
/// <param name="Loader">限定的加载器（可空）。</param>
/// <param name="Offset">偏移。</param>
/// <param name="Limit">每页条数。</param>
/// <param name="Sort">排序字段。</param>
public sealed record ResourceSearchQuery(
    string Query,
    ResourceKind Kind = ResourceKind.Mod,
    string? GameVersion = null,
    ModLoaderKind? Loader = null,
    int Offset = 0,
    int Limit = 20,
    ResourceSortField Sort = ResourceSortField.Relevance);

/// <summary>社区资源索引（CurseForge / Modrinth 等）。</summary>
public interface IResourceIndex
{
    /// <summary>提供方名称。</summary>
    string Name { get; }

    /// <summary>检索资源。</summary>
    Task<Result<ResourceSearchResult>> SearchAsync(
        ResourceSearchQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>获取项目详情。</summary>
    Task<Result<ResourceProject>> GetProjectAsync(
        string idOrSlug,
        CancellationToken cancellationToken = default);

    /// <summary>获取可下载文件（可按游戏版本 / 加载器过滤）。</summary>
    Task<Result<IReadOnlyList<ResourceFile>>> GetFilesAsync(
        string idOrSlug,
        string? gameVersion = null,
        ModLoaderKind? loader = null,
        CancellationToken cancellationToken = default);
}