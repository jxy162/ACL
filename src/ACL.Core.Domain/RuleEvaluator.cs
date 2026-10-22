namespace ACL.Core.Domain;

/// <summary>Minecraft 启动规则（<c>rules</c>）求值（纯算法）。</summary>
/// <remarks>
/// 语义遵循 Minecraft：默认不允许，只有匹配到的规则才改变结果（后匹配者覆盖）；
/// 规则为 <see langword="null"/> 或空数组时视为允许。
/// <c>os.version</c> 正则匹配暂不实现（罕见且易误判）。
/// </remarks>
public static class RuleEvaluator
{
    /// <summary>判断在给定 OS 下是否允许。</summary>
    public static bool IsAllowed(IReadOnlyList<RuleEntry>? rules, OsSpec os)
    {
        ArgumentNullException.ThrowIfNull(os);

        if (rules is null || rules.Count == 0)
        {
            return true;
        }

        bool allowed = false;
        foreach (RuleEntry rule in rules)
        {
            if (Matches(rule, os))
            {
                allowed = rule.IsAllow;
            }
        }

        return allowed;
    }

    private static bool Matches(RuleEntry rule, OsSpec os)
    {
        if (rule.OsName is not null && !rule.OsName.Equals(os.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.OsArch is not null && !rule.OsArch.Equals(os.Arch, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}