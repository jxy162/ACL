using System.Diagnostics;
using ACL.Core.Abstractions;

namespace ACL.Core.Tasks;

/// <summary>
/// <see cref="ITaskPipeline"/> 的默认实现。
/// 语义：阶段默认串行；<see cref="ITaskStage.RunInParallel"/> 为真的阶段与前一阶段同批次并发执行；
/// 必需阶段失败即终止后续阶段（可选阶段失败不终止）。取消以 <see cref="OperationCanceledException"/> 传播。
/// </summary>
public sealed class TaskPipeline : ITaskPipeline
{
    /// <inheritdoc />
    public async Task<Result<TaskSummary>> RunAsync(
        string name,
        IReadOnlyList<ITaskStage> stages,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stages);
        string taskName = name ?? "task";

        ProgressNode root = new(taskName, 1d, progress);
        Dictionary<string, object?> items = new(StringComparer.Ordinal);
        List<StageOutcome> outcomes = [];
        ErrorCode failure = ErrorCode.None;
        string? failureDetail = null;
        bool aborted = false;

        foreach (IReadOnlyList<ITaskStage> batch in BuildBatches(stages))
        {
            if (aborted)
            {
                foreach (ITaskStage stage in batch)
                {
                    outcomes.Add(new StageOutcome(stage.Name, true, ErrorCode.Canceled, "流水线已终止。", TimeSpan.Zero));
                }

                continue;
            }

            List<Task<StageOutcome>> running = [];
            foreach (ITaskStage stage in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProgressNode node = root.Add(stage.Name, stage.Weight);
                TaskStageContext context = new(stage.Name, node, node, items);
                running.Add(RunStageAsync(stage, context, cancellationToken));
            }

            StageOutcome[] results = await Task.WhenAll(running).ConfigureAwait(false);
            for (int i = 0; i < batch.Count; i++)
            {
                ITaskStage stage = batch[i];
                StageOutcome outcome = results[i];
                outcomes.Add(outcome);

                if (stage.IsRequired && outcome.Code != ErrorCode.None)
                {
                    aborted = true;
                    if (failure == ErrorCode.None)
                    {
                        failure = outcome.Code;
                        failureDetail = outcome.Detail;
                    }
                }
            }
        }

        bool success = failure == ErrorCode.None;
        TaskSummary summary = new(taskName, outcomes, success, failure);

        // 失败时仍返回汇总（Value 非空），便于宿主做诊断与展示部分结果。
        return new Result<TaskSummary>(summary, failure, success ? null : failureDetail);
    }

    private static List<IReadOnlyList<ITaskStage>> BuildBatches(IReadOnlyList<ITaskStage> stages)
    {
        List<List<ITaskStage>> batches = [];
        foreach (ITaskStage stage in stages)
        {
            if (stage.RunInParallel && batches.Count > 0)
            {
                batches[^1].Add(stage);
            }
            else
            {
                batches.Add([stage]);
            }
        }

        return batches.ConvertAll(static b => (IReadOnlyList<ITaskStage>)b);
    }

    private static async Task<StageOutcome> RunStageAsync(
        ITaskStage stage,
        TaskStageContext context,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int attempt = 0;
        while (true)
        {
            attempt++;
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await stage.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
                return new StageOutcome(stage.Name, false, ErrorCode.None, null, stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (StageException ex)
            {
                if (attempt < stage.Retry.MaxAttempts && stage.Retry.ShouldRetry(ex.Code))
                {
                    await Task.Delay(stage.Retry.DelayAfter(attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                stopwatch.Stop();
                return new StageOutcome(stage.Name, false, ex.Code, ex.Message, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                if (attempt < stage.Retry.MaxAttempts && stage.Retry.ShouldRetry(ErrorCode.Unknown))
                {
                    await Task.Delay(stage.Retry.DelayAfter(attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                stopwatch.Stop();
                return new StageOutcome(stage.Name, false, ErrorCode.Unknown, ex.Message, stopwatch.Elapsed);
            }
        }
    }
}