using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.DoOnFailure;

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
