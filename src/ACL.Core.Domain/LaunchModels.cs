namespace ACL.Core.Domain;

/// <summary>一次启动的计划（dry-run 可完全确定，无需真实启动）。</summary>
/// <param name="JavaPath">Java 可执行文件路径。</param>
/// <param name="JvmArgs">JVM 参数（已去重、已替换模板）。</param>
/// <param name="GameArgs">游戏参数（已去重、已替换模板）。</param>
/// <param name="WorkingDir">工作目录。</param>
/// <param name="NativesDir">natives 目录。</param>
/// <param name="EnvVars">附加环境变量。</param>
public sealed record LaunchPlan(
    string JavaPath,
    IReadOnlyList<string> JvmArgs,
    IReadOnlyList<string> GameArgs,
    string WorkingDir,
    string NativesDir,
    IReadOnlyDictionary<string, string> EnvVars);