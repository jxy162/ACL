namespace ACL.Core.Abstractions;

/// <summary>
/// 可注入时钟。内核禁止直接读取 <see cref="DateTime.Now"/> / <see cref="DateTimeOffset.Now"/>。
/// </summary>
public interface ISystemClock
{
    /// <summary>当前 UTC 时间。</summary>
    DateTimeOffset UtcNow { get; }
}