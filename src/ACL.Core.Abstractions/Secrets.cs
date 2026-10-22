namespace ACL.Core.Abstractions;

/// <summary>密钥提供方（环境变量 / 配置 / CI secrets）。内核禁止硬编码密钥。</summary>
public interface ISecretProvider
{
    /// <summary>按键获取密钥；不存在返回 <see langword="null"/>。</summary>
    string? GetSecret(string key);
}

/// <summary>密钥保护器（DPAPI / libsecret / Keychain 抽象）。</summary>
public interface ISecretProtector
{
    /// <summary>保护明文（返回可落盘文本）。</summary>
    Result<string> Protect(string plaintext);

    /// <summary>还原明文。</summary>
    Result<string> Unprotect(string protectedText);
}