using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Planning;

internal static class PlannerCommon
{
    internal static Func<Options, Task<object>> CreateEvaluateUpstream<TIn>(Ast.INode<TIn> upstream)
        => async options => await Interpreter.ExecuteAsync(upstream, options);
}

internal sealed record PlannedFrame<TOut>(Ast.INode<TOut>? UpstreamNode,
    Func<Options, Task<object>>? EvaluateUpstream,
    Continuations.IContinuation<TOut> Continuation);
