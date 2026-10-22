using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Auth;

/// <summary>会话令牌存储（访问 / 刷新令牌必须加密落盘，见 docs/09）。</summary>
public interface ITokenStore
{
    /// <summary>读取会话；不存在返回 <see cref="ErrorCode.NotFound"/>。</summary>
    Result<AuthSession> Load(string accountId);

    /// <summary>保存会话。</summary>
    Result Save(string accountId, AuthSession session);

    /// <summary>删除会话（退出登录）。</summary>
    Result Delete(string accountId);
}