namespace BahmanM.Flow.Execution.Trampoline;

internal sealed class DoOnSuccessCont<T>(Flow.Operations.DoOnSuccess.Sync<T> action) : IContinuation<T>
{
    public Task<FrameResult<T>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Success<T> s)
        {
            try
            {
                action(s.Value);
                return Task.FromResult<FrameResult<T>>(new OutcomeResult<T>(s));
            }
            catch (Exception ex)
            {
                return Task.FromResult<FrameResult<T>>(new OutcomeResult<T>(Outcome.Failure<T>(ex)));
            }
        }
        return Task.FromResult<FrameResult<T>>(new OutcomeResult<T>((Outcome<T>)outcome));
    }
}

internal sealed class DoOnSuccessAsyncCont<T>(Flow.Operations.DoOnSuccess.Async<T> action) : IContinuation<T>
{
    public async Task<FrameResult<T>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Success<T> s)
        {
            try
            {
                await action(s.Value);
                return new OutcomeResult<T>(s);
            }
            catch (Exception ex)
            {
                return new OutcomeResult<T>(Outcome.Failure<T>(ex));
            }
        }
        return new OutcomeResult<T>((Outcome<T>)outcome);
    }
}

internal sealed class DoOnSuccessCancellableCont<T>(Flow.Operations.DoOnSuccess.CancellableAsync<T> action) : IContinuation<T>
{
    public async Task<FrameResult<T>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Success<T> s)
        {
            try
            {
                await action(s.Value, options.CancellationToken);
                return new OutcomeResult<T>(s);
            }
            catch (Exception ex)
            {
                return new OutcomeResult<T>(Outcome.Failure<T>(ex));
            }
        }
        return new OutcomeResult<T>((Outcome<T>)outcome);
    }
}

internal sealed class DoOnFailureCont<T>(Flow.Operations.DoOnFailure.Sync action) : IContinuation<T>
{
    public Task<FrameResult<T>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Failure<T> f)
        {
            try { action(f.Exception); } catch { /* swallow */ }
            return Task.FromResult<FrameResult<T>>(new OutcomeResult<T>(f));
        }
        return Task.FromResult<FrameResult<T>>(new OutcomeResult<T>((Outcome<T>)outcome));
    }
}

internal sealed class DoOnFailureAsyncCont<T>(Flow.Operations.DoOnFailure.Async action) : IContinuation<T>
{
    public async Task<FrameResult<T>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Failure<T> f)
        {
            try { await action(f.Exception); } catch { /* swallow */ }
            return new OutcomeResult<T>(f);
        }
        return new OutcomeResult<T>((Outcome<T>)outcome);
    }
}

internal sealed class DoOnFailureCancellableCont<T>(Flow.Operations.DoOnFailure.CancellableAsync action) : IContinuation<T>
{
    public async Task<FrameResult<T>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Failure<T> f)
        {
            try { await action(f.Exception, options.CancellationToken); } catch { /* swallow */ }
            return new OutcomeResult<T>(f);
        }
        return new OutcomeResult<T>((Outcome<T>)outcome);
    }
}

