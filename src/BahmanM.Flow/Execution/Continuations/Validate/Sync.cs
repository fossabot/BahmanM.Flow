using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.Validate;

internal sealed class ValidateCont<T>(Func<T, bool> predicate, Func<T, Exception> exceptionFactory) : IContinuation<T>
{
    public Task<FrameResult<T>> ApplyAsync(object outcome, Execution.Options options)
    {
        if (outcome is Success<T> s)
        {
            try
            {
                if (predicate(s.Value))
                {
                    return Task.FromResult<FrameResult<T>>(new OutcomeResult<T>(s));
                }
                var ex = exceptionFactory(s.Value);
                return Task.FromResult<FrameResult<T>>(new OutcomeResult<T>(Outcome.Failure<T>(ex)));
            }
            catch (Exception ex)
            {
                return Task.FromResult<FrameResult<T>>(new OutcomeResult<T>(Outcome.Failure<T>(ex)));
            }
        }
        return Task.FromResult<FrameResult<T>>(new OutcomeResult<T>((Outcome<T>)outcome));
    }
}
