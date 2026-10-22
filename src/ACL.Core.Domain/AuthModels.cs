namespace ACL.Core.Domain;

/// <summary>账号类型。</summary>
public enum AccountType
{
    /// <summary>微软正版。</summary>
    Microsoft,

    /// <summary>离线。</summary>
    Offline,

    /// <summary>第三方（Yggdrasil / authlib-injector）。</summary>
    ThirdParty,
}

/// <summary>一次认证会话。</summary>
/// <param name="Type">账号类型。</param>
/// <param name="Uuid">玩家 UUID（无连字符小写）。</param>
/// <param name="Name">玩家名。</param>
/// <param name="AccessToken">访问令牌。</param>
/// <param name="RefreshToken">刷新令牌（可选）。</param>
/// <param name="ExpiresAt">过期时间（UTC，可选）。</param>
public sealed record AuthSession(
    AccountType Type,
    string Uuid,
    string Name,
    string AccessToken,
    string? RefreshToken = null,
    DateTimeOffset? ExpiresAt = null);