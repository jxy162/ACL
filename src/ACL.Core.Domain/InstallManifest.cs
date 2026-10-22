namespace ACL.Core.Domain;

/// <summary>安装清单中的单个受管文件。</summary>
/// <param name="RelativePath">相对实例目录的路径。</param>
/// <param name="Sha1">SHA-1 校验值（小写十六进制）。</param>
/// <param name="LastWriteTimeUtc">写入时间（UTC），用于篡改检测。</param>
/// <param name="Component">归属组件标识（可选）。</param>
public sealed record ManagedFile(
    string RelativePath,
    string Sha1,
    DateTimeOffset LastWriteTimeUtc,
    string? Component = null);

/// <summary>实例安装清单：记录组件集合与受管文件，用于补全与篡改检测。</summary>
/// <param name="SchemaVersion">清单结构版本。</param>
/// <param name="Components">组件集合。</param>
/// <param name="LastModifiedUtc">清单最后修改时间（UTC）。</param>
/// <param name="ManagedFiles">受管文件列表。</param>
public sealed record InstallManifest(
    int SchemaVersion,
    ComponentSet Components,
    DateTimeOffset LastModifiedUtc,
    IReadOnlyList<ManagedFile> ManagedFiles)
{
    /// <summary>当前清单结构版本。</summary>
    public const int CurrentSchemaVersion = 1;
}