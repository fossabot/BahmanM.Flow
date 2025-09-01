using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.Recover;

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
