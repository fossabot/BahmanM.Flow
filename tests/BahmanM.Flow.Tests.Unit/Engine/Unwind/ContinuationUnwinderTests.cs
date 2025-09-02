namespace BahmanM.Flow.Tests.Unit.Engine.Unwind;

using BahmanM.Flow.Execution.Engine;
using BahmanM.Flow.Execution.Engine.Unwind;
using BahmanM.Flow.Execution.Continuations;
using BahmanM.Flow.Execution.Continuations.Select;

public class ContinuationUnwinderTests
{
    [Fact]
    public async Task Unwind_AppliesOutcomeResultFramesInOrder()
    {
        var continuations = new Stack<IContinuation<int>>();
        continuations.Push(new SelectCont<int, int>(x => x + 1));
        continuations.Push(new SelectCont<int, int>(x => x * 2));

        var state = await ContinuationUnwinder.UnwindAsync(continuations, Outcome.Success(3), new BahmanM.Flow.Execution.Options(CancellationToken.None));
        Assert.Null(state.NextNode);
        var final = Assert.IsType<Success<int>>(state.FinalOutcome);
        // ((3 * 2) + 1) = 7
        Assert.Equal(7, final.Value);
    }

    [Fact]
    public async Task Unwind_OnPushFlow_ReturnsNextNode()
    {
        var continuations = new Stack<IContinuation<int>>();
        // Use Recover to force a PushFlow on failure
        var recover = new BahmanM.Flow.Execution.Continuations.Recover.RecoverCont<int>(_ => Flow.Succeed(42));
        continuations.Push(recover);

        var state = await ContinuationUnwinder.UnwindAsync(continuations, Outcome.Failure<int>(new Exception("x")), new BahmanM.Flow.Execution.Options(CancellationToken.None));
        Assert.NotNull(state.NextNode);
        Assert.Null(state.FinalOutcome);
    }
}
