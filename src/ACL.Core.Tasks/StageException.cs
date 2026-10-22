using ACL.Core.Abstractions;

namespace ACL.Core.Tasks;

/// <summary>阶段以预期失败（<see cref="ErrorCode"/>）结束的异常。</summary>
public sealed class StageException : Exception
{
    /// <summary>构造阶段异常。</summary>
    /// <param name="code">错误码。</param>
    /// <param name="message">仅用于诊断的细节。</param>
    /// <param name="innerException">内部异常。</param>
    public StageException(ErrorCode code, string? message = null, Exception? innerException = null)
        : base(message ?? $"阶段失败：{code}。", innerException)
    {
        Code = code;
    }

    /// <summary>错误码。</summary>
    public ErrorCode Code { get; }
}