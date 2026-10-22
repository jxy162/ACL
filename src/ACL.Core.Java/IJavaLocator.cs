using ACL.Core.Domain;

namespace ACL.Core.Java;

/// <summary>Java 运行时搜索：在给定的搜索根下发现并探测 Java。</summary>
public interface IJavaLocator
{
    /// <summary>搜索并返回已探测成功的 Java 列表（按版本降序）。</summary>
    Task<IReadOnlyList<JavaRuntime>> LocateAsync(CancellationToken cancellationToken = default);
}