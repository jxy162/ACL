using ACL.Core.Abstractions;

namespace ACL.Core.Tasks;

/// <summary>
/// 加权进度树节点。叶子节点上报自身比例；分支节点的比例为其子节点按权重加权的平均。
/// 叶子节点的比例单调不减（上报较小值不会回退进度）。
/// </summary>
public sealed class ProgressNode : IProgressSink
{
    private readonly List<ProgressNode> _children = [];
    private readonly ProgressNode? _parent;
    private readonly IProgressSink? _rootSink;
    private double _self;

    /// <summary>构造进度节点。</summary>
    /// <param name="name">节点名。</param>
    /// <param name="weight">相对权重（&gt;0）。</param>
    /// <param name="sink">根节点对外上报的接收端（仅根节点需要）。</param>
    /// <param name="parent">父节点（内部使用）。</param>
    public ProgressNode(string name, double weight = 1, IProgressSink? sink = null, ProgressNode? parent = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Weight = weight > 0 ? weight : 1;
        _parent = parent;
        _rootSink = parent is null ? sink : null;
    }

    /// <summary>节点名。</summary>
    public string Name { get; }

    /// <summary>相对权重。</summary>
    public double Weight { get; }

    /// <summary>子节点集合。</summary>
    public IReadOnlyList<ProgressNode> Children => _children;

    /// <summary>是否为叶子节点。</summary>
    public bool IsLeaf => _children.Count == 0;

    /// <summary>当前完成比例，取值 [0,1]。</summary>
    public double Fraction => IsLeaf ? _self : ComputeFromChildren();

    /// <summary>新增一个子节点。</summary>
    public ProgressNode Add(string name, double weight = 1)
    {
        ProgressNode child = new(name, weight, parent: this);
        _children.Add(child);
        return child;
    }

    /// <summary>上报本节点比例（单调不减）。</summary>
    public void Report(double fraction)
    {
        double clamped = Math.Clamp(fraction, 0d, 1d);
        if (clamped > _self)
        {
            _self = clamped;
        }

        Notify();
    }

    /// <summary>标记完成（比例为 1）。</summary>
    public void Complete() => Report(1d);

    void IProgressSink.Report(ProgressReport report) => Report(report.Fraction);

    private double ComputeFromChildren()
    {
        double weighted = 0d;
        double totalWeight = 0d;
        foreach (ProgressNode child in _children)
        {
            weighted += child.Weight * child.Fraction;
            totalWeight += child.Weight;
        }

        return totalWeight <= 0d ? 0d : weighted / totalWeight;
    }

    private void Notify()
    {
        if (_parent is not null)
        {
            _parent.Notify();
            return;
        }

        _rootSink?.Report(new ProgressReport(Name, Fraction));
    }
}