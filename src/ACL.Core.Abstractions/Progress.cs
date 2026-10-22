namespace ACL.Core.Abstractions;

/// <summary>一次进度上报：阶段名、归一化到 [0,1] 的分值与可选消息。</summary>
/// <param name="Stage">阶段名称。</param>
/// <param name="Fraction">完成比例，取值 [0,1]。</param>
/// <param name="Message">可选的人类可读消息（不承载业务文案）。</param>
public readonly record struct ProgressReport(string Stage, double Fraction, string? Message = null);

/// <summary>
/// 进度接收端。内核只负责上报，界面 / CLI 各自渲染。
/// </summary>
public interface IProgressSink
{
    /// <summary>上报一次进度。</summary>
    void Report(ProgressReport report);
}

/// <summary><see cref="IProgressSink"/> 的便捷上报扩展。</summary>
public static class ProgressSinkExtensions
{
    /// <summary>以阶段名与比例上报进度。</summary>
    public static void Report(this IProgressSink sink, string stage, double fraction, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Report(new ProgressReport(stage, fraction, message));
    }
}