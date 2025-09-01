using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.DoOnFailure;

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
