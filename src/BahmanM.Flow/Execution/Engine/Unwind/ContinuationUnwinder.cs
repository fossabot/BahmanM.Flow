using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;

namespace BahmanM.Flow.Execution.Engine.Unwind;

internal static class ContinuationUnwinder
{
    internal static async Task<UnwindState<T>> UnwindAsync<T>(Stack<IContinuation<T>> conts, object currentOutcome, Options options)
    {
        var outcome = currentOutcome;
        while (conts.Count > 0)
        {
            var cont = conts.Pop();
            var res = await cont.ApplyAsync(outcome, options);
            switch (res)
            {
                case Engine.OutcomeResult<T> r:
                    outcome = r.Outcome; continue;
                case Engine.PushFlow<T> p:
                    return new UnwindState<T>((INode<T>)p.Flow, null);
                default:
                    throw new NotSupportedException("Unknown frame result type.");
            }
        }
        return new UnwindState<T>(null, outcome);
    }
}

