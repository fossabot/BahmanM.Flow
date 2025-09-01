using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.Select;

internal sealed class SelectCont<TIn, TOut>(Flow.Operations.Select.Sync<TIn, TOut> selector) : IContinuation<TOut>
{
    public Task<FrameResult<TOut>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Success<TIn> s)
        {
            try
            {
                var value = selector(s.Value);
                return Task.FromResult<FrameResult<TOut>>(new OutcomeResult<TOut>(Outcome.Success(value)));
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
