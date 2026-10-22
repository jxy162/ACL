using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Modpack;

/// <summary>把整合包中缺少直链的文件解析为可下载文件（如 CurseForge 的 projectID/fileID）。</summary>
public interface IModpackFileResolver
{
    /// <summary>解析清单中未解析的文件；返回补齐后的文件列表（顺序与 <see cref="ModpackManifest.UnresolvedFiles"/> 对应）。</summary>
    Task<Result<IReadOnlyList<ModpackFile>>> ResolveAsync(
        ModpackManifest manifest,
        CancellationToken cancellationToken = default);
}