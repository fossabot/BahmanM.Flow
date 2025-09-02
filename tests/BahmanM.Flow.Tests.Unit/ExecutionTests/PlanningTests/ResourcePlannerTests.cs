using BahmanM.Flow.Execution;
using BahmanM.Flow.Execution.Planning.Resource;

namespace BahmanM.Flow.Tests.Unit.ExecutionTests.PlanningTests;

public class ResourcePlannerTests
{
    private sealed class Dummy : IDisposable
    {
        public static int Disposed;
        public void Dispose() => Interlocked.Increment(ref Disposed);
    }

    [Fact]
    public async Task ResourcePlanner_TryCreate_Acquire_Use_And_Dispose()
    {
        Dummy.Disposed = 0;
        var flow = Flow.WithResource(() => new Dummy(), _ => Flow.Succeed(7));

        var node = flow.AsNode();
        Assert.True(ResourcePlanner.TryCreate(node, out var plan));

        var res = plan.Acquire();
        Assert.IsType<Dummy>(res);

        var used = plan.Use(res);
        Assert.NotNull(used);

        var disposeCont = plan.CreateDisposeCont(res);
        var result = await disposeCont.ApplyAsync(Outcome.Success(123), new BahmanM.Flow.Execution.Options(CancellationToken.None));

        Assert.Equal(1, Dummy.Disposed);
        var outcomeResult = Assert.IsType<BahmanM.Flow.Execution.Engine.OutcomeResult<int>>(result);
        Assert.IsType<Success<int>>(outcomeResult.Outcome);
    }
}
