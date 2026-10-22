using ACL.Core.Abstractions;

namespace ACL.Infrastructure;

/// <summary>基于系统时钟的 <see cref="ISystemClock"/> 实现。</summary>
public sealed class SystemClock : ISystemClock
{
    /// <summary>共享实例。</summary>
    public static SystemClock Instance { get; } = new();

    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}