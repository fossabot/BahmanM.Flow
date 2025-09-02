namespace BahmanM.Flow.Tests.Unit.Engine.Concurrency;

using BahmanM.Flow.Execution;
using BahmanM.Flow.Execution.Engine;
using BahmanM.Flow.Execution.Engine.Concurrency;

public class ConcurrencyExecutorTests
{
    [Fact]
    public async Task All_WhenAllSucceed_ReturnsArrayInOrder()
    {
        var flow = Flow.All(Flow.Succeed(1), Flow.Succeed(2), Flow.Succeed(3));

        var obj = await ConcurrencyExecutor.TryHandleAsync(flow.AsNode(), new Options(CancellationToken.None));
        var outcome = Assert.IsAssignableFrom<Outcome<int[]>>(obj);
        var success = Assert.IsType<Success<int[]>>(outcome);
        Assert.Equal(new[] { 1, 2, 3 }, success.Value);
    }

    [Fact]
    public async Task All_WhenAnyFails_ReturnsAggregateException()
    {
        var flow = Flow.All(Flow.Fail<int>(new InvalidOperationException()), Flow.Succeed(2));

        var obj = await ConcurrencyExecutor.TryHandleAsync(flow.AsNode(), new Options(CancellationToken.None));
        var failure = Assert.IsType<Failure<int[]>>(obj);
        Assert.IsType<AggregateException>(failure.Exception);
    }

    [Fact]
    public async Task Any_ReturnsFirstSuccessfulOutcome()
    {
        var slowSuccess = Flow.Create<int>(new Func<Task<int>>(async () => { await Task.Delay(30); return 1; }));
        var fastSuccess = Flow.Create<int>(new Func<Task<int>>(async () => { await Task.Delay(5); return 2; }));
        var failing = Flow.Create<int>(new Func<Task<int>>(async () => { await Task.Delay(1); throw new Exception("boom"); }));

        var flow = Flow.Any(slowSuccess, failing, fastSuccess);
        var obj = await ConcurrencyExecutor.TryHandleAsync(flow.AsNode(), new Options(CancellationToken.None));

        var outcome = Assert.IsAssignableFrom<Outcome<int>>(obj);
        var success = Assert.IsType<Success<int>>(outcome);
        Assert.Equal(2, success.Value);
    }
}
