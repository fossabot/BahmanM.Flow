using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;
using BahmanM.Flow.Execution.Planning;

namespace BahmanM.Flow.Execution.Engine.Planning;

internal static class FramePlanning
{
    internal static async Task<PlanResult<T>> TryPlanAsync<T>(INode<T> currentNode, Stack<IContinuation<T>> continuations, Options options)
    {
        if (NodePlanner.TryPlan(currentNode, out var frame))
        {
            continuations.Push(frame.Continuation);
            if (frame.UpstreamNode is not null)
            {
                return new PlanResult<T>(true, frame.UpstreamNode, null);
            }
            if (frame.EvaluateUpstream is not null)
            {
                var outcome = await frame.EvaluateUpstream(options);
                return new PlanResult<T>(true, null, outcome);
            }
        }
        return new PlanResult<T>(false, null, null);
    }
}
