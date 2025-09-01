using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.DoOnSuccess;

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
