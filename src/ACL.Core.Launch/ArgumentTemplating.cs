using System.Text;
using System.Text.RegularExpressions;

namespace ACL.Core.Launch;

/// <summary>启动参数模板变量替换与去重。</summary>
public static partial class ArgumentTemplating
{
    /// <summary>替换 <c>${var}</c> 占位符；未知变量保持原样（便于诊断）。</summary>
    public static string Expand(string value, IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(variables);

        return PlaceholderPattern().Replace(value, match =>
        {
            string key = match.Groups[1].Value;
            return variables.TryGetValue(key, out string? replacement) ? replacement : match.Value;
        });
    }

    /// <summary>逐个替换。</summary>
    public static List<string> Expand(IEnumerable<string> values, IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(values);
        return [.. values.Select(value => Expand(value, variables))];
    }

    /// <summary>
    /// 去掉重复的 <c>-</c> 开头参数（保留首次出现的那个），重复时连带跳过其紧随的取值。
    /// 去重按"参数键"判定：<c>-Xmx*</c> / <c>-Xms*</c> 视为同键，<c>-D&lt;键&gt;=值</c> 按 <c>-D&lt;键&gt;</c> 判定，其余按整串判定。
    /// </summary>
    public static List<string> DistinctPreservingOrder(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        List<string> source = [.. arguments];
        List<string> result = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        for (int i = 0; i < source.Count; i++)
        {
            string token = source[i];
            if (!token.StartsWith("-", StringComparison.Ordinal))
            {
                result.Add(token);
                continue;
            }

            if (!seen.Add(FlagKey(token)))
            {
                // 跳过重复参数及其紧随的取值（若已配过对）
                if (i + 1 < source.Count && !source[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    i++;
                }

                continue;
            }

            result.Add(token);
        }

        return result;
    }

    private static string FlagKey(string token)
    {
        if (token.StartsWith("-Xmx", StringComparison.Ordinal) || token.StartsWith("-Xms", StringComparison.Ordinal))
        {
            return token[..4];
        }

        if (token.StartsWith("-D", StringComparison.Ordinal))
        {
            int equals = token.IndexOf('=', StringComparison.Ordinal);
            return equals > 0 ? token[..equals] : token;
        }

        return token;
    }

    [GeneratedRegex("\\$\\{([^}]+)\\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();
}