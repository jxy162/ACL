namespace ACL.Core.Domain;

/// <summary>一个已探测到的 Java 运行时。</summary>
/// <param name="HomeFolder">Java 主目录（JAVA_HOME 语义）。</param>
/// <param name="Version">Java 版本号，如 <c>17.0.9</c>。</param>
/// <param name="Is64Bit">是否为 64 位。</param>
/// <param name="FileEncoding">文件编码（可为空）。</param>
/// <param name="NativeEncoding">本地编码（可为空）。</param>
public sealed record JavaRuntime(
    string HomeFolder,
    Version Version,
    bool Is64Bit,
    string? FileEncoding = null,
    string? NativeEncoding = null);

/// <summary>半开区间 <c>[MinInclusive, MaxExclusive)</c> 的 Java 版本范围。</summary>
/// <param name="MinInclusive">含下界；<see langword="null"/> 表示不限。</param>
/// <param name="MaxExclusive">不含上界；<see langword="null"/> 表示不限。</param>
public sealed record JavaVersionRange(Version? MinInclusive = null, Version? MaxExclusive = null)
{
    /// <summary>是否包含指定版本。</summary>
    public bool Contains(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return (MinInclusive is null || version >= MinInclusive)
            && (MaxExclusive is null || version < MaxExclusive);
    }
}

/// <summary>对 Java 的需求。</summary>
/// <param name="Range">可接受的版本范围。</param>
/// <param name="RecommendedComponent">推荐用于安装的组件标识（可选）。</param>
public sealed record JavaRequirement(JavaVersionRange Range, string? RecommendedComponent = null);