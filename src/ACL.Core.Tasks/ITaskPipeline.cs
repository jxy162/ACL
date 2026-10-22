using ACL.Core.Abstractions;

namespace ACL.Core.Tasks;

/// <summary>任务流水线：把复杂流程拆成带权重的阶段，统一进度、取消、重试与失败传播。</summary>
public interface ITaskPipeline
{
    /// <summary>按顺序（或按并行批次）执行阶段。</summary>
    /// <param name="name">流水线名。</param>
    /// <param name="stages">阶段列表。</param>
    /// <param name="progress">进度接收端（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>
    /// 返回 <see cref="TaskSummary"/>；存在必需阶段失败时 <c>Code</c> 非 <see cref="ErrorCode.None"/>，
    /// 但 <c>Value</c> 仍携带部分汇总，便于宿主诊断。
    /// </returns>
    Task<Result<TaskSummary>> RunAsync(
        string name,
        IReadOnlyList<ITaskStage> stages,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default);
}