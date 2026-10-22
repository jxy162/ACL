using ACL.Core.Abstractions;

namespace ACL.Core.Tasks;

/// <summary>阶段执行上下文：阶段名、阶段级进度接收端、进度节点与跨阶段共享数据袋。</summary>
public sealed class TaskStageContext
{
    /// <summary>构造上下文。</summary>
    /// <param name="stageName">阶段名。</param>
    /// <param name="sink">阶段级进度接收端（上报到本阶段进度节点）。</param>
    /// <param name="progress">本阶段进度节点。</param>
    /// <param name="items">跨阶段共享数据袋。</param>
    public TaskStageContext(string stageName, IProgressSink sink, ProgressNode progress, IDictionary<string, object?> items)
    {
        StageName = stageName ?? throw new ArgumentNullException(nameof(stageName));
        Sink = sink ?? throw new ArgumentNullException(nameof(sink));
        Progress = progress ?? throw new ArgumentNullException(nameof(progress));
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    /// <summary>阶段名。</summary>
    public string StageName { get; }

    /// <summary>阶段级进度接收端。</summary>
    public IProgressSink Sink { get; }

    /// <summary>本阶段进度节点（可继续派生子节点）。</summary>
    public ProgressNode Progress { get; }

    /// <summary>跨阶段共享数据袋（阶段间传递中间产物）。</summary>
    public IDictionary<string, object?> Items { get; }
}