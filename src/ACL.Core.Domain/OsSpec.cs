namespace ACL.Core.Domain;

/// <summary>目标操作系统规格（用于 <see cref="RuleEntry"/> 匹配）。</summary>
/// <param name="Name">名称：windows / linux / osx。</param>
/// <param name="Arch">架构：x86 / x86_64 / arm64。</param>
public sealed record OsSpec(string Name, string Arch = "x86_64")
{
    /// <summary>Windows x64。</summary>
    public static OsSpec WindowsX64 { get; } = new("windows", "x86_64");

    /// <summary>Linux x64。</summary>
    public static OsSpec LinuxX64 { get; } = new("linux", "x86_64");

    /// <summary>macOS（osx）x64。</summary>
    public static OsSpec MacOsX64 { get; } = new("osx", "x86_64");
}