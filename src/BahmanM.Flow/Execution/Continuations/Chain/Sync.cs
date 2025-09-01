using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.Chain;

internal sealed class ChainCont<TIn, TOut>(Flow.Operations.Chain.Sync<TIn, TOut> op) : IContinuation<TOut>
{
    public Task<FrameResult<TOut>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Success<TIn> s)
        {
            try
            {
                var flow = op(s.Value);
                return Task.FromResult<FrameResult<TOut>>(new PushFlow<TOut>(flow));
            }
            catch (Exception ex)
            {
                return Task.FromResult<FrameResult<TOut>>(new OutcomeResult<TOut>(Outcome.Failure<TOut>(ex)));
            }
        }
        var failure = (Failure<TIn>)outcome;
        return Task.FromResult<FrameResult<TOut>>(new OutcomeResult<TOut>(Outcome.Failure<TOut>(failure.Exception)));
    }
}
