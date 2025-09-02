using BahmanM.Flow.Ast;

namespace BahmanM.Flow.Execution.Planning;

internal static class NodePlanner
{
    internal static bool TryPlan<TOut>(INode<TOut> node, out PlannedFrame<TOut> plan)
    {
        if (Select.SelectPlanner.TryPlan(node, out plan)) return true;
        if (Chain.ChainPlanner.TryPlan(node, out plan)) return true;
        if (Validate.ValidatePlanner.TryPlan(node, out plan)) return true;
        if (DoOnSuccess.DoOnSuccessPlanner.TryPlan(node, out plan)) return true;
        if (DoOnFailure.DoOnFailurePlanner.TryPlan(node, out plan)) return true;
        if (Recover.RecoverPlanner.TryPlan(node, out plan)) return true;

        plan = null!;
        return false;
    }
}
