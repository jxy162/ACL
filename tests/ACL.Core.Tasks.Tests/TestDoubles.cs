using ACL.Core.Abstractions;

namespace ACL.Core.Tasks.Tests;

/// <summary>记录所有上报的进度接收端（线程安全）。</summary>
internal sealed class RecordingProgressSink : IProgressSink
{
    private readonly List<ProgressReport> _reports = [];

    public IReadOnlyList<ProgressReport> Reports
    {
        get
        {
            lock (_reports)
            {
                return _reports.ToArray();
            }
        }
    }

    public void Report(ProgressReport report)
    {
        lock (_reports)
        {
            _reports.Add(report);
        }
    }
}