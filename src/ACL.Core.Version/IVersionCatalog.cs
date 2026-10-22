using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>版本清单目录：获取并解析官方 / 镜像的版本清单。</summary>
public interface IVersionCatalog
{
    /// <summary>获取版本清单（多源并发取先返回者）。</summary>
    Task<Result<VersionManifest>> GetManifestAsync(CancellationToken cancellationToken = default);
}