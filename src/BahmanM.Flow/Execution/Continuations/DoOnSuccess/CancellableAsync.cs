using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.DoOnSuccess;

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
