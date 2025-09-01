using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Continuations;

internal interface IContinuation<TOut>
{
    Task<FrameResult<TOut>> ApplyAsync(object outcome, Options options);
}
