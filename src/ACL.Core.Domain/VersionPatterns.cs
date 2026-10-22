using System.Text.RegularExpressions;

namespace ACL.Core.Domain;

/// <summary>版本名解析用的共享正则（源生成，避免运行时编译开销）。</summary>
internal static partial class VersionPatterns
{
    /// <summary>把版本名切分为字母段与数字段。</summary>
    [GeneratedRegex("[a-z]+|[0-9]+")]
    internal static partial Regex Token();

    /// <summary>匹配开头的点分数字段，如 <c>1.20.1</c>、<c>26.1</c>。</summary>
    [GeneratedRegex("^[0-9]+(?:\\.[0-9]+)*")]
    internal static partial Regex LeadingDottedNumbers();

    /// <summary>匹配旧式快照名，如 <c>16w01a</c>。</summary>
    [GeneratedRegex("^[0-9]{2}w[0-9]{2}[a-z]?$")]
    internal static partial Regex LegacySnapshot();
}