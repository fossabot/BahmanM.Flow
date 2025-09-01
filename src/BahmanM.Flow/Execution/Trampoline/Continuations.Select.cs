namespace BahmanM.Flow.Execution.Trampoline;

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

internal sealed class SelectAsyncCont<TIn, TOut>(Flow.Operations.Select.Async<TIn, TOut> selector) : IContinuation<TOut>
{
    public async Task<FrameResult<TOut>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Success<TIn> s)
        {
            try
            {
                var value = await selector(s.Value);
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

