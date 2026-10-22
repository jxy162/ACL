namespace ACL.Core.Domain;

/// <summary>某个 Minecraft 版本的规格描述。</summary>
/// <param name="Vanilla">原版版本名。</param>
/// <param name="Comparable">三段式可比较版本号。</param>
/// <param name="Drop">Drop 序数（用于排序 / 筛选）。</param>
/// <param name="IsSnapshot">是否为快照。</param>
/// <param name="IsFool">是否为愚人节版本。</param>
/// <param name="ReleaseTime">发布时间（UTC）。</param>
/// <param name="Reliable">版本号是否来自可靠依据（非猜测）。</param>
public sealed record McVersionSpec(
    VersionName Vanilla,
    Version Comparable,
    int Drop,
    bool IsSnapshot,
    bool IsFool,
    DateTimeOffset ReleaseTime,
    bool Reliable)
{
    /// <summary>由版本名与发布时间构造规格。</summary>
    /// <param name="name">版本名。</param>
    /// <param name="releaseTime">发布时间。</param>
    /// <param name="isFool">是否为愚人节版本。</param>
    /// <param name="reliable">是否可靠；<see langword="null"/> 时依据是否解析出数字版本号判定。</param>
    public static McVersionSpec FromName(
        string name,
        DateTimeOffset releaseTime,
        bool isFool = false,
        bool? reliable = null)
    {
        VersionNumbers parsed = VersionParsing.Parse(name);
        return new McVersionSpec(
            new VersionName(name),
            parsed.Comparable,
            parsed.Drop,
            parsed.IsSnapshot,
            isFool,
            releaseTime,
            reliable ?? parsed.HasNumbers);
    }
}