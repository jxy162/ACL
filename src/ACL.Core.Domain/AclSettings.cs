namespace ACL.Core.Domain;

/// <summary>启动器设置（便携存储为 <c>ACL.config.json</c>）。</summary>
public sealed record AclSettings
{
    /// <summary>结构版本（用于未来迁移）。</summary>
    public int SchemaVersion { get; init; } = 2;

    /// <summary>最小堆（MB）。</summary>
    public int MinMemoryMb { get; init; } = 512;

    /// <summary>最大堆（MB）。</summary>
    public int MaxMemoryMb { get; init; } = 4096;

    /// <summary>是否优先官方源（否则镜像优先）。</summary>
    public bool PreferOfficialMirror { get; init; } = true;

    /// <summary>玩家名（离线登录）。</summary>
    public string PlayerName { get; init; } = "Steve";

    /// <summary>上次选择的版本。</summary>
    public string? SelectedVersionId { get; init; }

    /// <summary>当前使用的账号 ID（指向 <c>ACL.accounts.json</c>）。</summary>
    public string? CurrentAccountId { get; init; }

    /// <summary>主题：<c>dark</c> / <c>light</c> / <c>system</c>。</summary>
    public string Theme { get; init; } = "dark";

    /// <summary>是否减弱动效（可访问性）。</summary>
    public bool ReduceMotion { get; init; }

    /// <summary>各版本累计游玩时长（分钟）。</summary>
    public Dictionary<string, double> PlayMinutes { get; init; } = [];

    /// <summary>各实例的独立设置（实例名 → 设置）。</summary>
    public Dictionary<string, InstanceSettings> Instances { get; init; } = [];
}