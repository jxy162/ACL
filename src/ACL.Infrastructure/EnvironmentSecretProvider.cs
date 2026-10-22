using ACL.Core.Abstractions;

namespace ACL.Infrastructure;

/// <summary>从环境变量读取密钥的 <see cref="ISecretProvider"/> 实现。</summary>
public sealed class EnvironmentSecretProvider : ISecretProvider
{
    private readonly string? _prefix;

    /// <summary>构造提供方。</summary>
    /// <param name="prefix">可选键前缀（如 <c>ACL_</c>）。</param>
    public EnvironmentSecretProvider(string? prefix = null)
    {
        _prefix = prefix;
    }

    /// <inheritdoc />
    public string? GetSecret(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        string? value = Environment.GetEnvironmentVariable(_prefix + key);
        return string.IsNullOrEmpty(value) ? null : value;
    }
}