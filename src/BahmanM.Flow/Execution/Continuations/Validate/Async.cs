using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.Validate;

internal sealed class ValidateAsyncCont<T>(Func<T, Task<bool>> predicate, Func<T, Exception> exceptionFactory) : IContinuation<T>
{
    public async Task<FrameResult<T>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Success<T> s)
        {
            try
            {
                if (await predicate(s.Value))
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
