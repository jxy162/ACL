namespace ACL.Core.Domain;

/// <summary>
/// 单个游戏实例的独立设置（覆盖全局设置；为空表示沿用全局值）。
/// </summary>
public sealed record InstanceSettings
{
    /// <summary>最小堆（MB；可空）。</summary>
    public int? MinMemoryMb { get; init; }

    /// <summary>最大堆（MB；可空）。</summary>
    public int? MaxMemoryMb { get; init; }

    /// <summary>指定的 Java 可执行文件路径（可空：自动选择）。</summary>
    public string? JavaPath { get; init; }

    /// <summary>附加 JVM 参数（原样追加）。</summary>
    public string? ExtraJvmArgs { get; init; }

    /// <summary>附加游戏参数（原样追加）。</summary>
    public string? ExtraGameArgs { get; init; }

    /// <summary>自定义工作目录（可空：使用游戏根目录）。</summary>
    public string? WorkingDir { get; init; }

    /// <summary>备注。</summary>
    public string? Note { get; init; }
}