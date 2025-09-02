using BahmanM.Flow.Execution;
using BahmanM.Flow.Execution.Planning.Chain;

namespace BahmanM.Flow.Tests.Unit.ExecutionTests.PlanningTests;

public class ChainPlannerTests
{
    [Fact]
    public void ChainPlanner_SameType_Uses_UpstreamNode()
    {
        var source = Flow.Succeed(1);
        var flow = source.Chain(x => Flow.Succeed(x + 1));

        var node = flow.AsNode();
        var ok = ChainPlanner.TryPlan(node, out var plan);
        Assert.True(ok);
        Assert.NotNull(plan.UpstreamNode);
        Assert.Null(plan.EvaluateUpstream);
    }

    [Fact]
    public void ChainPlanner_DifferentType_Uses_EvaluateUpstream()
    {
        var source = Flow.Succeed(1);
        var flow = source.Chain<int, string>(x => Flow.Succeed(x.ToString()));

        var node = flow.AsNode();
        var ok = ChainPlanner.TryPlan(node, out var plan);
        Assert.True(ok);
        Assert.Null(plan.UpstreamNode);
        Assert.NotNull(plan.EvaluateUpstream);
    }
}
