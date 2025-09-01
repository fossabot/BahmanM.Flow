using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.DoOnSuccess;

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
