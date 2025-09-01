using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.Validate;

internal sealed class ValidateCancellableCont<T>(Func<T, CancellationToken, Task<bool>> predicate, Func<T, Exception> exceptionFactory) : IContinuation<T>
{
    public async Task<FrameResult<T>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (options.CancellationToken.IsCancellationRequested)
        {
            return new OutcomeResult<T>(Outcome.Failure<T>(new TaskCanceledException()));
        }

        if (outcome is Success<T> s)
        {
            try
            {
                if (await predicate(s.Value, options.CancellationToken))
                {
                    return new OutcomeResult<T>(s);
                }
                var ex = exceptionFactory(s.Value);
                return new OutcomeResult<T>(Outcome.Failure<T>(ex));
            }
            catch (Exception ex)
            {
                return new OutcomeResult<T>(Outcome.Failure<T>(ex));
            }
        }
        return new OutcomeResult<T>((Outcome<T>)outcome);
    }
}
