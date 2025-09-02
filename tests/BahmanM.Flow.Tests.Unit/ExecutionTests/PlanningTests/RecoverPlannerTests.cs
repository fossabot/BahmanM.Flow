using BahmanM.Flow.Execution;
using BahmanM.Flow.Execution.Planning.Recover;

namespace BahmanM.Flow.Tests.Unit.ExecutionTests.PlanningTests;

public class RecoverPlannerTests
{
    [Theory]
    [InlineData("sync")]
    [InlineData("async")]
    [InlineData("cancellable")]
    public void RecoverPlanner_Chooses_Correct_Continuation_And_Source_As_Upstream(string variant)
    {
        IFlow<int> source = Flow.Fail<int>(new Exception("boom"));
        IFlow<int> flow = variant switch
        {
            "sync" => source.Recover(ex => Flow.Succeed(42)),
            "async" => source.Recover(async ex => { await Task.Yield(); return Flow.Succeed(42); }),
            _ => source.Recover(async (ex, ct) => { await Task.Yield(); return Flow.Succeed(42); })
        };

        var node = flow.AsNode();
        Assert.True(RecoverPlanner.TryPlan(node, out var plan));

        Assert.NotNull(plan.UpstreamNode);
        Assert.Null(plan.EvaluateUpstream);
        Assert.True(object.ReferenceEquals(plan.UpstreamNode, source.AsNode()));

        var name = plan.Continuation.GetType().Name;
        switch (variant)
        {
            case "sync": Assert.StartsWith("RecoverCont", name); break;
            case "async": Assert.StartsWith("RecoverAsyncCont", name); break;
            default: Assert.StartsWith("RecoverCancellableCont", name); break;
        }
    }
}
