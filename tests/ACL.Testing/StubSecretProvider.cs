using ACL.Core.Abstractions;

namespace ACL.Testing;

/// <summary>固定返回同一密钥的提供方桩。</summary>
public sealed class StubSecretProvider : ISecretProvider
{
    private readonly string? _value;

    public StubSecretProvider(string? value) => _value = value;

    public string? GetSecret(string key) => _value;
}