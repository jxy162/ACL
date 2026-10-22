using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Resources;

/// <summary>本地资源扫描：从 jar 读取 Fabric / Quilt / Forge / NeoForge / 旧版元数据。</summary>
public interface ILocalResourceScanner
{
    /// <summary>扫描单个 jar。</summary>
    Result<LocalResourceInfo> Scan(string jarPath);

    /// <summary>扫描目录下所有 jar。</summary>
    Result<IReadOnlyList<LocalResourceInfo>> ScanDirectory(string directory);
}