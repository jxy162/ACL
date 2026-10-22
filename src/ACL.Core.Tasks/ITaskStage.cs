namespace ACL.Core.Tasks;

/// <summary>任务流水线中的一个阶段。</summary>
public interface ITaskStage
{
    /// <summary>阶段名。</summary>
    string Name { get; }

    /// <summary>进度权重（&gt;0）。</summary>
    double Weight { get; }

    /// <summary>是否与前一阶段并行（在同一批次内并发执行）。</summary>
    bool RunInParallel { get; }

    /// <summary>是否为必需阶段（失败会导致流水线终止）。</summary>
    bool IsRequired { get; }

    /// <summary>重试策略。</summary>
    RetryPolicy Retry { get; }

    /// <summary>执行阶段。</summary>
    ValueTask ExecuteAsync(TaskStageContext context, CancellationToken cancellationToken);
}

/// <summary>阶段基类，提供默认的权重 / 必需 / 并行 / 重试设置。</summary>
public abstract class TaskStage : ITaskStage
{
    /// <inheritdoc />
    public required string Name { get; init; }

    /// <inheritdoc />
    public double Weight { get; init; } = 1;

    /// <inheritdoc />
    public bool RunInParallel { get; init; }

    /// <inheritdoc />
    public bool IsRequired { get; init; } = true;

    /// <inheritdoc />
    public RetryPolicy Retry { get; init; } = RetryPolicy.None;

    /// <inheritdoc />
    public abstract ValueTask ExecuteAsync(TaskStageContext context, CancellationToken cancellationToken);
}

/// <summary>以委托实现的阶段。</summary>
public sealed class DelegateTaskStage : TaskStage
{
    private readonly Func<TaskStageContext, CancellationToken, ValueTask> _body;

    /// <summary>构造委托阶段。</summary>
    public DelegateTaskStage(Func<TaskStageContext, CancellationToken, ValueTask> body)
    {
        _body = body ?? throw new ArgumentNullException(nameof(body));
    }

    /// <inheritdoc />
    public override ValueTask ExecuteAsync(TaskStageContext context, CancellationToken cancellationToken)
        => _body(context, cancellationToken);
}