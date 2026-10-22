using ACL.Core.Domain;

namespace ACL.Core.Java;

/// <summary>Java 选择：按需求从候选运行时尚中挑出最合适的一个。</summary>
public interface IJavaSelector
{
    /// <summary>选择 64 位、满足版本范围且版本最高的运行时；无匹配返回 <see langword="null"/>。</summary>
    JavaRuntime? Select(IEnumerable<JavaRuntime> runtimes, JavaRequirement requirement);
}