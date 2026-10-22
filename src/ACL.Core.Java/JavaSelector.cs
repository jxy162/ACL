using ACL.Core.Domain;

namespace ACL.Core.Java;

/// <summary><see cref="IJavaSelector"/> 的默认实现（拒绝 32 位）。</summary>
public sealed class JavaSelector : IJavaSelector
{
    /// <inheritdoc />
    public JavaRuntime? Select(IEnumerable<JavaRuntime> runtimes, JavaRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(runtimes);
        ArgumentNullException.ThrowIfNull(requirement);

        return runtimes
            .Where(runtime => runtime.Is64Bit)
            .Where(runtime => requirement.Range.Contains(runtime.Version))
            .OrderByDescending(runtime => runtime.Version)
            .FirstOrDefault();
    }
}