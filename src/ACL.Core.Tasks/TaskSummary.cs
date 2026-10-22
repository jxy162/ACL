using ACL.Core.Abstractions;

namespace ACL.Core.Tasks;

/// <summary>单个阶段的执行结果。</summary>
/// <param name="Name">阶段名。</param>
/// <param name="Skipped">是否因流水线终止而跳过。</param>
/// <param name="Code">结果错误码（<see cref="ErrorCode.None"/> 表示成功）。</param>
/// <param name="Detail">诊断细节（不展示给用户）。</param>
/// <param name="Duration">耗时。</param>
public sealed record StageOutcome(string Name, bool Skipped, ErrorCode Code, string? Detail, TimeSpan Duration);

/// <summary>一次流水线运行的汇总。</summary>
/// <param name="Name">流水线名。</param>
/// <param name="Stages">各阶段结果。</param>
/// <param name="Success">是否成功（无必需阶段失败）。</param>
/// <param name="Code">首个必需阶段失败的错误码；成功时为 <see cref="ErrorCode.None"/>。</param>
public sealed record TaskSummary(string Name, IReadOnlyList<StageOutcome> Stages, bool Success, ErrorCode Code);