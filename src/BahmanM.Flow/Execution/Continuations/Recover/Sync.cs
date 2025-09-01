using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.Recover;

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
