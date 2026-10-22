using ACL.Core.Abstractions;

namespace ACL.Core.Launch;

/// <summary>natives 解压：把库的 natives 归档解压到实例的 natives 目录。</summary>
public interface INativesExtractor
{
    /// <summary>解压多个归档，返回写入的文件总数。</summary>
    /// <param name="jarPaths">归档路径（不存在的会被跳过）。</param>
    /// <param name="nativesDirectory">目标目录。</param>
    /// <param name="exclusions">需要排除的条目前缀（默认排除 <c>META-INF/</c>）。</param>
    Result<int> Extract(
        IReadOnlyList<string> jarPaths,
        string nativesDirectory,
        IReadOnlyList<string>? exclusions = null);
}