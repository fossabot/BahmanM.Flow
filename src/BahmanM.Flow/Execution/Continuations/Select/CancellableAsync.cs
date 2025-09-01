using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.Select;

internal sealed class SelectCancellableCont<TIn, TOut>(Flow.Operations.Select.CancellableAsync<TIn, TOut> selector) : IContinuation<TOut>
{
    public async Task<FrameResult<TOut>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (options.CancellationToken.IsCancellationRequested)
        {
            return new OutcomeResult<TOut>(Outcome.Failure<TOut>(new TaskCanceledException()));
        }

        if (outcome is Success<TIn> s)
        {
            try
            {
                var value = await selector(s.Value, options.CancellationToken);
                return new OutcomeResult<TOut>(Outcome.Success(value));
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
