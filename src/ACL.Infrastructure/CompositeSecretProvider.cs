using ACL.Core.Abstractions;

namespace ACL.Infrastructure;

/// <summary>按顺序查询多个密钥源，返回首个命中的值。</summary>
public sealed class CompositeSecretProvider : ISecretProvider
{
    private readonly IReadOnlyList<ISecretProvider> _providers;

    /// <summary>构造组合提供方。</summary>
    public CompositeSecretProvider(params ISecretProvider[] providers)
    {
        _providers = providers ?? [];
    }

    /// <inheritdoc />
    public string? GetSecret(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        foreach (ISecretProvider provider in _providers)
        {
            string? value = provider.GetSecret(key);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }
}