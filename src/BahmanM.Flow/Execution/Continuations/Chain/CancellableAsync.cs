using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.Chain;

internal sealed class ChainCancellableCont<TIn, TOut>(Flow.Operations.Chain.CancellableAsync<TIn, TOut> op) : IContinuation<TOut>
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
                var flow = await op(s.Value, options.CancellationToken);
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
