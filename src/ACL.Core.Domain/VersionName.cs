using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ACL.Core.Domain;

/// <summary>
/// 封装 Minecraft 版本名（如 <c>1.20.1</c>、<c>1.7.10-pre4</c>、<c>26.1-snapshot-1</c>）
/// 并提供稳定的比较语义：左侧较新返回正数，相同返回 0。
/// </summary>
/// <remarks>
/// 比较规则（本项目独立实现）：把版本名归一化后切分为字母 / 数字段逐段比较；
/// 关键字段 <c>rc</c>/<c>pre</c>/<c>snapshot</c>/<c>experimental</c> 视作低于同位置的数字；
/// 缺失的段按 <c>0</c> 处理；字母段之间的顺序按序数比较。
/// </remarks>
public sealed record VersionName
{
    private static readonly Dictionary<string, int> KeywordWeights = new(StringComparer.Ordinal)
    {
        ["rc"] = -1,
        ["pre"] = -2,
        ["snapshot"] = -3,
        ["experimental"] = -4,
    };

    /// <summary>以原始版本名构造。</summary>
    /// <param name="raw">版本名原文。</param>
    public VersionName(string raw)
    {
        Raw = raw ?? throw new ArgumentNullException(nameof(raw));
    }

    /// <summary>原始版本名。</summary>
    public string Raw { get; }

    /// <summary>是否为占位 / 未知版本名。</summary>
    [JsonIgnore]
    public bool IsUnknown => string.IsNullOrWhiteSpace(Raw) || IsUnknownToken(Raw);

    /// <inheritdoc />
    public int CompareTo(VersionName? other) => other is null ? 1 : Compare(Raw, other.Raw);

    /// <inheritdoc />
    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        VersionName other => CompareTo(other),
        _ => throw new ArgumentException("对象不是 VersionName。", nameof(obj)),
    };

    /// <summary>本版本是否不旧于 <paramref name="other"/>。</summary>
    public bool IsAtLeast(VersionName other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return CompareTo(other) >= 0;
    }

    /// <summary>比较两个版本名（左侧较新返回正数，相同返回 0，右侧较新返回负数）。</summary>
    public static int Compare(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 0;
        }

        string l = Normalize(left);
        string r = Normalize(right);
        if (l.Length == 0 || r.Length == 0)
        {
            return string.CompareOrdinal(l, r);
        }

        IReadOnlyList<string> lp = Tokenize(l);
        IReadOnlyList<string> rp = Tokenize(r);
        int count = Math.Max(lp.Count, rp.Count);
        for (int i = 0; i < count; i++)
        {
            string lv = i < lp.Count ? lp[i] : "0";
            string rv = i < rp.Count ? rp[i] : "0";
            if (string.Equals(lv, rv, StringComparison.Ordinal))
            {
                continue;
            }

            int lw = Weight(lv);
            int rw = Weight(rv);
            if (lw == 0 && rw == 0)
            {
                return string.CompareOrdinal(lv, rv);
            }

            if (lw != rw)
            {
                return lw < rw ? -1 : 1;
            }
        }

        return string.CompareOrdinal(l, r);
    }

    private static bool IsUnknownToken(string value)
        => value is "未知版本" || value.Equals("unknown", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value)
    {
        string s = value.Trim().ToLowerInvariant();
        return s
            .Replace("快照", "snapshot", StringComparison.Ordinal)
            .Replace("预览版", "pre", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> Tokenize(string normalized)
    {
        List<string> tokens = [];
        foreach (Match match in VersionPatterns.Token().Matches(normalized))
        {
            tokens.Add(match.Value);
        }

        return tokens;
    }

    private static int Weight(string token)
    {
        if (KeywordWeights.TryGetValue(token, out int weight))
        {
            return weight;
        }

        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : 0;
    }
}