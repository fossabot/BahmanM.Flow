using BahmanM.Flow.Execution;
using BahmanM.Flow.Execution.Planning.Select;

namespace BahmanM.Flow.Tests.Unit.ExecutionTests.PlanningTests;

public class SelectPlannerTests
{
    [Theory]
    [InlineData(false, false)] // sync only
    [InlineData(true, false)]  // async present
    [InlineData(false, true)]  // cancellable present
    public void SelectPlanner_Fuses_SameType_And_Chooses_Minimal_Continuation(bool includeAsync, bool includeCancellable)
    {
        var flow = Flow.Succeed(1)
            .Select<int, int>(x => x + 1);

        if (includeAsync)
            flow = flow.Select<int, int>(async x => { await Task.Yield(); return x + 1; });
        if (includeCancellable)
            flow = flow.Select<int, int>(async (x, ct) => { await Task.Yield(); return x + 1; });

        var node = flow.AsNode();
        Assert.True(SelectPlanner.TryPlan(node, out var plan));

        Assert.NotNull(plan.UpstreamNode);
        Assert.Null(plan.EvaluateUpstream);

        var contTypeName = plan.Continuation.GetType().Name;
        if (includeCancellable)
            Assert.StartsWith("SelectCancellableCont", contTypeName);
        else if (includeAsync)
            Assert.StartsWith("SelectAsyncCont", contTypeName);
        else
            Assert.StartsWith("SelectCont", contTypeName);
    }

    [Fact]
    public void SelectPlanner_DifferentType_Uses_EvaluateUpstream_And_Single_Continuation()
    {
        var flow = Flow.Succeed(1).Select<int, string>(x => x.ToString());
        var node = flow.AsNode();

        Assert.True(SelectPlanner.TryPlan(node, out var plan));

        Assert.Null(plan.UpstreamNode);
        Assert.NotNull(plan.EvaluateUpstream);
        Assert.StartsWith("SelectCont", plan.Continuation.GetType().Name);
    }
}
