using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;
using BahmanM.Flow.Execution.Planning;

namespace BahmanM.Flow.Execution.Engine.Planning;

internal static class FramePlanning
{
    internal static async Task<PlanResult<T>> TryPlanAsync<T>(INode<T> node, Stack<IContinuation<T>> conts, Options options)
    {
        if (NodePlanner.TryPlan(node, out var plan))
        {
            conts.Push(plan.Continuation);
            if (plan.UpstreamNode is not null)
            {
                return new PlanResult<T>(true, plan.UpstreamNode, null);
            }
            if (plan.EvaluateUpstream is not null)
            {
                var outcome = await plan.EvaluateUpstream(options);
                return new PlanResult<T>(true, null, outcome);
            }
        }
        return new PlanResult<T>(false, null, null);
    }
}

