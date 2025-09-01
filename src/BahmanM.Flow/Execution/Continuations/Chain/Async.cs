using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.Chain;

internal sealed class ChainAsyncCont<TIn, TOut>(Flow.Operations.Chain.Async<TIn, TOut> op) : IContinuation<TOut>
{
    public async Task<FrameResult<TOut>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Success<TIn> s)
        {
            try
            {
                var flow = await op(s.Value);
                return new PushFlow<TOut>(flow);
            }
            catch (Exception ex)
            {
                return new OutcomeResult<TOut>(Outcome.Failure<TOut>(ex));
            }
        }
        var failure = (Failure<TIn>)outcome;
        return new OutcomeResult<TOut>(Outcome.Failure<TOut>(failure.Exception));
    }
}
