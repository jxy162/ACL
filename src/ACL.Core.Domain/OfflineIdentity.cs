using System.Security.Cryptography;
using System.Text;

namespace ACL.Core.Domain;

/// <summary>离线身份的确定性 UUID 生成（纯算法）。</summary>
public static class OfflineIdentity
{
    /// <summary>
    /// 由用户名生成确定性 UUID：<c>MD5("OfflinePlayer:" + name)</c>，并按 RFC 4122 置版本位（3）与变体位。
    /// </summary>
    public static string CreateUuid(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + name));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);   // 版本 3
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);   // RFC 4122 变体
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>生成离线会话（UUID 与访问令牌均由用户名确定性推导）。</summary>
    public static AuthSession CreateSession(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string uuid = CreateUuid(name);
        return new AuthSession(AccountType.Offline, uuid, name, uuid);
    }
}