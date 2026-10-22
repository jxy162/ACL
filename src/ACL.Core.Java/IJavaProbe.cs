using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Java;

/// <summary>Java 运行时探测：运行 <c>java -XshowSettings:properties -version</c> 并解析版本 / 位数 / 编码。</summary>
public interface IJavaProbe
{
    /// <summary>探测指定 Java 可执行文件。</summary>
    Task<Result<JavaRuntime>> ProbeAsync(string javaExecutablePath, CancellationToken cancellationToken = default);
}