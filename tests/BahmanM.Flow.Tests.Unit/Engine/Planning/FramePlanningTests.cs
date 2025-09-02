namespace BahmanM.Flow.Tests.Unit.Engine.Planning;

using BahmanM.Flow.Execution;
using BahmanM.Flow.Execution.Engine;
using BahmanM.Flow.Execution.Engine.Planning;
using BahmanM.Flow.Execution.Continuations;

public class FramePlanningTests
{
    [Fact]
    public async Task TryPlanAsync_Select_SameType_ReturnsNextNode_And_PushesContinuation()
    {
        var flow = Flow.Succeed(1).Select<int, int>(x => x + 1);
        var node = flow.AsNode();
        var continuations = new Stack<IContinuation<int>>();

        var result = await FramePlanning.TryPlanAsync(node, continuations, new BahmanM.Flow.Execution.Options(CancellationToken.None));
        Assert.True(result.Handled);
        Assert.NotNull(result.NextNode);
        Assert.Null(result.Outcome);
        Assert.Single(continuations);
    }

    [Fact]
    public async Task TryPlanAsync_Select_DifferentType_EvaluatesUpstream_And_PushesContinuation()
    {
        var flow = Flow.Succeed(7).Select<int, string>(x => x.ToString());
        var node = flow.AsNode();
        var continuations = new Stack<IContinuation<string>>();

        var result = await FramePlanning.TryPlanAsync(node, continuations, new BahmanM.Flow.Execution.Options(CancellationToken.None));
        Assert.True(result.Handled);
        Assert.Null(result.NextNode);
        var upstreamOutcome = Assert.IsAssignableFrom<Outcome<int>>(result.Outcome);
        var success = Assert.IsType<Success<int>>(upstreamOutcome);
        Assert.Equal(7, success.Value);
        Assert.Single(continuations);
    }
}
