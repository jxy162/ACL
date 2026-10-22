using ACL.Core.Abstractions;
using Avalonia.Threading;

namespace ACL.App.ViewModels;

/// <summary>把内核进度回调切回 UI 线程的接收端。</summary>
public sealed class UiProgressSink : IProgressSink
{
    private readonly Action<ProgressReport> _onReport;

    /// <summary>构造接收端。</summary>
    public UiProgressSink(Action<ProgressReport> onReport) => _onReport = onReport;

    /// <inheritdoc />
    public void Report(ProgressReport report) => Dispatcher.UIThread.Post(() => _onReport(report));
}