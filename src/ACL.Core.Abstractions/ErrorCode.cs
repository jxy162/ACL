namespace ACL.Core.Abstractions;

/// <summary>
/// 内核稳定错误码。内核不内联用户文案，宿主负责把错误码映射为本地化提示。
/// </summary>
public enum ErrorCode
{
    /// <summary>成功。</summary>
    None = 0,

    /// <summary>网络请求失败。</summary>
    Network,

    /// <summary>请求超时。</summary>
    Timeout,

    /// <summary>下载文件校验值不匹配。</summary>
    HashMismatch,

    /// <summary>资源或版本未找到。</summary>
    NotFound,

    /// <summary>镜像不可用。</summary>
    MirrorUnavailable,

    /// <summary>被服务方限流（HTTP 429），应退避后重试。</summary>
    RateLimited,

    /// <summary>认证令牌过期（可尝试刷新）。</summary>
    AuthExpired,

    /// <summary>认证被拒绝。</summary>
    AuthDenied,

    /// <summary>应用未通过服务方审核 / Client 注册无效。</summary>
    InvalidAppRegistration,

    /// <summary>未找到可用的 Java 运行时。</summary>
    JavaMissing,

    /// <summary>Java 版本与目标不兼容。</summary>
    JavaIncompatible,

    /// <summary>版本名解析失败。</summary>
    VersionParseFailed,

    /// <summary>清单或元数据损坏。</summary>
    ManifestCorrupt,

    /// <summary>进程启动失败。</summary>
    ProcessStartFailed,

    /// <summary>游戏进程异常退出（崩溃）。</summary>
    GameCrashed,

    /// <summary>配置无效。</summary>
    ConfigInvalid,

    /// <summary>IO 错误。</summary>
    IoError,

    /// <summary>操作被取消（正常控制流）。</summary>
    Canceled,

    /// <summary>未知错误。</summary>
    Unknown,
}