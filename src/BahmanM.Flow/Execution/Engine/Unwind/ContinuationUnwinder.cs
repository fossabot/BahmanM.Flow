using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;

namespace BahmanM.Flow.Execution.Engine.Unwind;

internal static class ContinuationUnwinder
{
    internal static async Task<UnwindState<T>> UnwindAsync<T>(Stack<IContinuation<T>> continuations, object currentOutcome, Options options)
    {
        var accumulatedOutcome = currentOutcome;
        while (continuations.Count > 0)
        {
            var continuation = continuations.Pop();
            var frameResult = await continuation.ApplyAsync(accumulatedOutcome, options);
            switch (frameResult)
            {
                case Engine.OutcomeResult<T> r:
                    accumulatedOutcome = r.Outcome; continue;
                case Engine.PushFlow<T> p:
                    return new UnwindState<T>((INode<T>)p.Flow, null);
                default:
                    throw new NotSupportedException("Unknown frame result type.");
            }
        }
        return new UnwindState<T>(null, accumulatedOutcome);
    }
}
