using ACL.Core.Abstractions;
using Xunit;

namespace ACL.Core.Tasks.Tests;

public class TaskPipelineTests
{
    [Fact]
    public async Task Stages_RunInOrder()
    {
        List<string> order = [];
        ITaskStage[] stages =
        [
            Stage("s1", (_, _) => { order.Add("s1"); return ValueTask.CompletedTask; }),
            Stage("s2", (_, _) => { order.Add("s2"); return ValueTask.CompletedTask; }),
            Stage("s3", (_, _) => { order.Add("s3"); return ValueTask.CompletedTask; }),
        ];

        Result<TaskSummary> result = await new TaskPipeline().RunAsync("p", stages);

        Assert.True(result.IsSuccess);
        Assert.Equal(["s1", "s2", "s3"], order);
    }

    [Fact]
    public async Task RunInParallel_StageRunsConcurrently()
    {
        TaskCompletionSource secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool firstCompleted = false;

        ITaskStage first = Stage("first", async (_, ct) =>
        {
            await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            firstCompleted = true;
        });

        ITaskStage second = new DelegateTaskStage((_, _) =>
        {
            secondStarted.TrySetResult();
            return ValueTask.CompletedTask;
        })
        {
            Name = "second",
            RunInParallel = true,
        };

        Task<Result<TaskSummary>> run = new TaskPipeline().RunAsync("p", [first, second]);
        Task finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.Same(run, finished);
        Result<TaskSummary> result = await run;
        Assert.True(result.IsSuccess);
        Assert.True(firstCompleted);
    }

    [Fact]
    public async Task Retry_RetriesUntilSuccess()
    {
        int attempts = 0;
        ITaskStage flaky = new DelegateTaskStage((_, _) =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new StageException(ErrorCode.Network, "boom");
            }

            return ValueTask.CompletedTask;
        })
        {
            Name = "flaky",
            Retry = new RetryPolicy(5, TimeSpan.FromMilliseconds(1)),
        };

        Result<TaskSummary> result = await new TaskPipeline().RunAsync("p", [flaky]);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task RequiredFailure_AbortsRemainingAndFails()
    {
        bool thirdRan = false;
        ITaskStage[] stages =
        [
            Stage("s1", (_, _) => ValueTask.CompletedTask),
            Stage("s2", (_, _) => throw new StageException(ErrorCode.HashMismatch, "bad")),
            Stage("s3", (_, _) => { thirdRan = true; return ValueTask.CompletedTask; }),
        ];

        Result<TaskSummary> result = await new TaskPipeline().RunAsync("p", stages);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.HashMismatch, result.Code);
        Assert.False(thirdRan);
        Assert.True(result.Value!.Stages.Single(s => s.Name == "s3").Skipped);
    }

    [Fact]
    public async Task OptionalFailure_DoesNotFailPipeline()
    {
        bool secondRan = false;
        ITaskStage[] stages =
        [
            new DelegateTaskStage((_, _) => throw new StageException(ErrorCode.NotFound, "opt"))
            {
                Name = "s1",
                IsRequired = false,
            },
            Stage("s2", (_, _) => { secondRan = true; return ValueTask.CompletedTask; }),
        ];

        Result<TaskSummary> result = await new TaskPipeline().RunAsync("p", stages);

        Assert.True(result.IsSuccess);
        Assert.True(secondRan);
        Assert.Equal(ErrorCode.NotFound, result.Value!.Stages.Single(s => s.Name == "s1").Code);
    }

    [Fact]
    public async Task Cancellation_Propagates()
    {
        ITaskStage slow = Stage("slow", async (_, ct) => await Task.Delay(TimeSpan.FromSeconds(30), ct));
        using CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        Task<Result<TaskSummary>> run = new TaskPipeline().RunAsync("p", [slow], null, cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    private static DelegateTaskStage Stage(string name, Func<TaskStageContext, CancellationToken, ValueTask> body)
        => new(body) { Name = name };
}