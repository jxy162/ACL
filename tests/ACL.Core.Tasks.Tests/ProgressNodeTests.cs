using Xunit;

namespace ACL.Core.Tasks.Tests;

public class ProgressNodeTests
{
    [Fact]
    public void BranchFraction_IsWeightedAverageOfChildren()
    {
        RecordingProgressSink sink = new();
        ProgressNode root = new("root", 1, sink);
        ProgressNode a = root.Add("a", 1);
        ProgressNode b = root.Add("b", 3);

        a.Complete();
        Assert.Equal(0.25d, root.Fraction, 3);

        b.Report(0.5);
        Assert.Equal(0.625d, root.Fraction, 3);

        b.Complete();
        Assert.Equal(1d, root.Fraction, 3);
        Assert.NotEmpty(sink.Reports);
    }

    [Fact]
    public void LeafFraction_IsMonotonic()
    {
        ProgressNode root = new("r", 1, new RecordingProgressSink());
        ProgressNode leaf = root.Add("l", 1);

        leaf.Report(0.8);
        leaf.Report(0.2);   // 不应回退
        Assert.Equal(0.8d, leaf.Fraction, 3);
    }

    [Fact]
    public void Report_ClampsOutOfRange()
    {
        ProgressNode leaf = new("l", 1, new RecordingProgressSink());
        leaf.Report(5);
        Assert.Equal(1d, leaf.Fraction, 3);
    }

    [Fact]
    public void RootSink_ReceivesNodeNameAndFraction()
    {
        RecordingProgressSink sink = new();
        ProgressNode root = new("pipeline", 1, sink);
        root.Add("s1", 1).Complete();

        Assert.All(sink.Reports, r => Assert.Equal("pipeline", r.Stage));
        Assert.Equal(1d, sink.Reports[^1].Fraction, 3);
    }
}