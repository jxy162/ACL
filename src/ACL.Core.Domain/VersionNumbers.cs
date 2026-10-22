using System.Globalization;
using System.Text.RegularExpressions;

namespace ACL.Core.Domain;

/// <summary>
/// 版本名到"可比较版本号 + Drop 序数"的解析结果（本项目独立实现）。
/// </summary>
/// <param name="Comparable">三段式可比较版本号。无版本号信息时为 9999.0.0。</param>
/// <param name="Drop">用于排序 / 筛选的 Drop 序数 <c>Major*10+Minor</c>；无版本号信息时为 209。</param>
/// <param name="IsSnapshot">是否为快照。</param>
/// <param name="HasNumbers">是否解析出可靠的版本号。</param>
public readonly record struct VersionNumbers(Version Comparable, int Drop, bool IsSnapshot, bool HasNumbers);

/// <summary>版本名解析。</summary>
public static class VersionParsing
{
    /// <summary>未识别版本号时使用的可比较版本。</summary>
    public static readonly Version UnknownComparable = new(9999, 0, 0);

    /// <summary>未识别版本号时使用的 Drop 序数。</summary>
    public const int UnknownDrop = 209;

    /// <summary>从版本名解析可比较版本号、Drop 序数并判断是否快照。</summary>
    public static VersionNumbers Parse(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        string lower = name.Trim().ToLowerInvariant();
        bool isLegacySnapshot = VersionPatterns.LegacySnapshot().IsMatch(lower);
        bool isSnapshot = isLegacySnapshot || lower.Contains("snapshot", StringComparison.Ordinal);
        if (isLegacySnapshot)
        {
            return new VersionNumbers(UnknownComparable, UnknownDrop, true, false);
        }

        int[] numbers = ReadLeadingNumbers(lower);
        if (numbers.Length == 0)
        {
            return new VersionNumbers(UnknownComparable, UnknownDrop, isSnapshot, false);
        }

        Version comparable = numbers[0] == 1 && numbers.Length >= 2
            ? new Version(numbers[1], 0, numbers.Length >= 3 ? numbers[2] : 0)
            : new Version(numbers[0], numbers.Length >= 2 ? numbers[1] : 0, numbers.Length >= 3 ? numbers[2] : 0);

        int drop = (comparable.Major * 10) + comparable.Minor;
        return new VersionNumbers(comparable, drop, isSnapshot, true);
    }

    private static int[] ReadLeadingNumbers(string value)
    {
        Match match = VersionPatterns.LeadingDottedNumbers().Match(value);
        if (!match.Success)
        {
            return [];
        }

        string[] segments = match.Value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        int[] result = new int[segments.Length];
        for (int i = 0; i < segments.Length; i++)
        {
            if (!int.TryParse(segments[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out result[i]))
            {
                return [];
            }
        }

        return result;
    }
}