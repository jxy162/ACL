namespace ACL.Core.Domain;

/// <summary>
/// 本地已登录账号档案（仅记录本机登录过的账号；令牌单独加密存放）。
/// </summary>
/// <param name="Id">稳定标识（正版为无连字符 UUID，离线为用户名小写）。</param>
/// <param name="Type">账号类型。</param>
/// <param name="Name">玩家名。</param>
/// <param name="Uuid">玩家 UUID（无连字符小写；可空）。</param>
/// <param name="LastUsedAt">最近使用时间（UTC）。</param>
public sealed record AccountProfile(
    string Id,
    AccountType Type,
    string Name,
    string? Uuid = null,
    DateTimeOffset LastUsedAt = default)
{
    /// <summary>类型展示文本。</summary>
    public string TypeLabel => Type switch
    {
        AccountType.Microsoft => "正版",
        AccountType.ThirdParty => "第三方",
        _ => "离线",
    };

    /// <summary>头像占位字符。</summary>
    public string AvatarLetter => string.IsNullOrWhiteSpace(Name) ? "A" : Name[..1].ToUpperInvariant();

    /// <summary>由离线用户名构造档案。</summary>
    public static AccountProfile Offline(string name, DateTimeOffset now)
        => new(name.ToLowerInvariant(), AccountType.Offline, name, OfflineIdentity.CreateUuid(name), now);

    /// <summary>由认证会话构造档案。</summary>
    public static AccountProfile FromSession(AuthSession session, DateTimeOffset now)
        => new(
            session.Type == AccountType.Offline ? session.Name.ToLowerInvariant() : session.Uuid,
            session.Type,
            session.Name,
            session.Uuid,
            now);
}