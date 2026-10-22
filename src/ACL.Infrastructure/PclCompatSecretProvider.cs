using ACL.Core.Abstractions;

namespace ACL.Infrastructure;

/// <summary>
/// 兼容映射：把 <c>ACL_XXX</c> 形式的键映射到既有前缀（如 <c>PCL_XXX</c>）再经环境变量读取，
/// 便于复用本机既有的用户级环境变量。
/// </summary>
public sealed class PrefixedCompatSecretProvider : ISecretProvider
{
    private readonly string _aclPrefix;
    private readonly string _legacyPrefix;
    private readonly ISecretProvider _inner;

    /// <summary>构造兼容提供方。</summary>
    /// <param name="inner">实际读取来源（通常为环境变量）。</param>
    /// <param name="aclPrefix">本项目前缀，默认 <c>ACL_</c>。</param>
    /// <param name="legacyPrefix">既有前缀，默认 <c>PCL_</c>。</param>
    public PrefixedCompatSecretProvider(ISecretProvider inner, string aclPrefix = "ACL_", string legacyPrefix = "PCL_")
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _aclPrefix = aclPrefix;
        _legacyPrefix = legacyPrefix;
    }

    /// <inheritdoc />
    public string? GetSecret(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (key.StartsWith(_aclPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return _inner.GetSecret(_legacyPrefix + key[_aclPrefix.Length..]);
        }

        return null;
    }
}