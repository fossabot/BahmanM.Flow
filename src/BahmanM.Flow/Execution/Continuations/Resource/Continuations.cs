using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations.Resource;

internal sealed class DisposeCont<TResource, T>(TResource resource) : IContinuation<T>
    where TResource : IDisposable
{
    public Task<FrameResult<T>> ApplyAsync(object outcome, Execution.Options options)
    {
        Exception? disposeEx = null;
        try
        {
            resource.Dispose();
        }
        catch (Exception ex)
        {
            disposeEx = ex;
        }

        if (disposeEx is not null)
        {
            return Task.FromResult<FrameResult<T>>(new OutcomeResult<T>(Outcome.Failure<T>(disposeEx)));
        }

        return Task.FromResult<FrameResult<T>>(new OutcomeResult<T>((Outcome<T>)outcome));
    }
}
