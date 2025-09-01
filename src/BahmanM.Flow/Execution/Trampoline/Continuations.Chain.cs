namespace BahmanM.Flow.Execution.Trampoline;

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

