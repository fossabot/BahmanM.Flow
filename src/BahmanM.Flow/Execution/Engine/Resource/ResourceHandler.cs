using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;
using BahmanM.Flow.Execution.Engine.Resource;

namespace BahmanM.Flow.Execution.Engine.Resource;

internal static class ResourceHandler
{
    internal static DescendEffect<T> TryHandle<T>(INode<T> node, Stack<IContinuation<T>> continuations)
    {
        var res = ResourceScope.TryOpen(node, continuations);
        if (!res.Handled) return new DescendEffect<T>(DescendEffectKind.NotHandled);
        if (res.NextNode is not null) return new DescendEffect<T>(DescendEffectKind.SetNextNode, NextNode: res.NextNode);
        if (res.Outcome is not null) return new DescendEffect<T>(DescendEffectKind.SetOutcome, Outcome: res.Outcome);
        return new DescendEffect<T>(DescendEffectKind.NotHandled);
    }
}
