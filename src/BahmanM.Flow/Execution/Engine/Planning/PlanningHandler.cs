using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;
using BahmanM.Flow.Execution.Engine.Planning;

namespace BahmanM.Flow.Execution.Engine.Planning;

internal static class PlanningHandler
{
    internal static async Task<DescendEffect<T>> TryHandleAsync<T>(INode<T> node, Stack<IContinuation<T>> continuations, Options options)
    {
        var plan = await FramePlanning.TryPlanAsync(node, continuations, options);
        if (!plan.Handled) return new DescendEffect<T>(DescendEffectKind.NotHandled);
        if (plan.NextNode is not null) return new DescendEffect<T>(DescendEffectKind.SetNextNode, NextNode: plan.NextNode);
        if (plan.Outcome is not null) return new DescendEffect<T>(DescendEffectKind.SetOutcome, Outcome: plan.Outcome);
        return new DescendEffect<T>(DescendEffectKind.NotHandled);
    }
}
