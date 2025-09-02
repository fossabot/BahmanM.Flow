using BahmanM.Flow.Execution;
using BahmanM.Flow.Execution.Planning.DoOnFailure;
using BahmanM.Flow.Execution.Planning.DoOnSuccess;

namespace BahmanM.Flow.Tests.Unit.ExecutionTests.PlanningTests;

public class DoOnPlannersTests
{
    [Theory]
    [InlineData("success", "sync")]
    [InlineData("success", "async")]
    [InlineData("success", "cancellable")]
    [InlineData("failure", "sync")]
    [InlineData("failure", "async")]
    [InlineData("failure", "cancellable")]
    public void DoOn_Planners_Select_Correct_Continuation(string side, string variant)
    {
        IFlow<int> flow = Flow.Succeed(1);

        IFlow<int> with;
        if (side == "success")
        {
            with = variant switch
            {
                "sync" => flow.DoOnSuccess(_ => { }),
                "async" => flow.DoOnSuccess(async _ => await Task.Yield()),
                _ => flow.DoOnSuccess(async (_, ct) => await Task.Yield())
            };
            Assert.True(DoOnSuccessPlanner.TryPlan(with.AsNode(), out var plan));
            Assert.NotNull(plan.UpstreamNode);
            Assert.Null(plan.EvaluateUpstream);
            var name = plan.Continuation.GetType().Name;
            if (variant == "sync") Assert.StartsWith("DoOnSuccessCont", name);
            else if (variant == "async") Assert.StartsWith("DoOnSuccessAsyncCont", name);
            else Assert.StartsWith("DoOnSuccessCancellableCont", name);
        }
        else
        {
            with = variant switch
            {
                "sync" => flow.DoOnFailure(_ => { }),
                "async" => flow.DoOnFailure(async _ => await Task.Yield()),
                _ => flow.DoOnFailure(async (_, ct) => await Task.Yield())
            };
            Assert.True(DoOnFailurePlanner.TryPlan(with.AsNode(), out var plan));
            Assert.NotNull(plan.UpstreamNode);
            Assert.Null(plan.EvaluateUpstream);
            var name = plan.Continuation.GetType().Name;
            if (variant == "sync") Assert.StartsWith("DoOnFailureCont", name);
            else if (variant == "async") Assert.StartsWith("DoOnFailureAsyncCont", name);
            else Assert.StartsWith("DoOnFailureCancellableCont", name);
        }
    }
}
