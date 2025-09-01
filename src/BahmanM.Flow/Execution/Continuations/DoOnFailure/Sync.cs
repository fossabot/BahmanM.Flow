using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.DoOnFailure;

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
