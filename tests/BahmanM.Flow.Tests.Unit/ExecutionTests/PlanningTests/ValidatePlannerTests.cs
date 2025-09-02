using BahmanM.Flow.Execution;
using BahmanM.Flow.Execution.Planning.Validate;

namespace BahmanM.Flow.Tests.Unit.ExecutionTests.PlanningTests;

public class ValidatePlannerTests
{
    [Theory]
    [InlineData("sync")]
    [InlineData("async")]
    [InlineData("cancellable")]
    public void ValidatePlanner_Chooses_Correct_Continuation(string variant)
    {
        IFlow<int> flow = Flow.Succeed(1);
        IFlow<int> nodeFlow = variant switch
        {
            "sync" => flow.Validate(x => x > 0, _ => new Exception()),
            "async" => flow.Validate(async x => { await Task.Yield(); return x > 0; }, _ => new Exception()),
            _ => flow.Validate(async (x, ct) => { await Task.Yield(); return x > 0; }, _ => new Exception())
        };

        var node = nodeFlow.AsNode();
        Assert.True(ValidatePlanner.TryPlan(node, out var plan));
        Assert.NotNull(plan.UpstreamNode);
        Assert.Null(plan.EvaluateUpstream);
        var name = plan.Continuation.GetType().Name;
        switch (variant)
        {
            case "sync": Assert.StartsWith("ValidateCont", name); break;
            case "async": Assert.StartsWith("ValidateAsyncCont", name); break;
            default: Assert.StartsWith("ValidateCancellableCont", name); break;
        }
    }
}
