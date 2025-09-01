namespace BahmanM.Flow.Execution.Trampoline;

internal sealed class RecoverCont<T>(Flow.Operations.Recover.Sync<T> recover) : IContinuation<T>
{
    public Task<FrameResult<T>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Failure<T> f)
        {
            try
            {
                var flow = recover(f.Exception);
                return Task.FromResult<FrameResult<T>>(new PushFlow<T>(flow));
            }
            catch (Exception ex)
            {
                return Task.FromResult<FrameResult<T>>(new OutcomeResult<T>(Outcome.Failure<T>(ex)));
            }
        }
        return Task.FromResult<FrameResult<T>>(new OutcomeResult<T>((Outcome<T>)outcome));
    }
}

internal sealed class RecoverAsyncCont<T>(Flow.Operations.Recover.Async<T> recover) : IContinuation<T>
{
    public async Task<FrameResult<T>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Failure<T> f)
        {
            try
            {
                var flow = await recover(f.Exception);
                return new PushFlow<T>(flow);
            }
            catch (Exception ex)
            {
                return new OutcomeResult<T>(Outcome.Failure<T>(ex));
            }
        }
        return new OutcomeResult<T>((Outcome<T>)outcome);
    }
}

internal sealed class RecoverCancellableCont<T>(Flow.Operations.Recover.CancellableAsync<T> recover) : IContinuation<T>
{
    public async Task<FrameResult<T>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Failure<T> f)
        {
            try
            {
                var flow = await recover(f.Exception, options.CancellationToken);
                return new PushFlow<T>(flow);
            }
            catch (Exception ex)
            {
                return new OutcomeResult<T>(Outcome.Failure<T>(ex));
            }
        }
        return new OutcomeResult<T>((Outcome<T>)outcome);
    }
}

