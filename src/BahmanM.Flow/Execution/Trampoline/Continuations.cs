using BahmanM.Flow.Execution;

namespace BahmanM.Flow.Execution.Trampoline;

internal interface IContinuation<TOut>
{
    Task<FrameResult<TOut>> ApplyAsync(object outcome, Options options);
}

