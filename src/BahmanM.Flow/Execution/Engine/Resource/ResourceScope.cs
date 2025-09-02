using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;

namespace BahmanM.Flow.Execution.Engine.Resource;

internal static class ResourceScope
{
    internal static ResourceResult<T> TryOpen<T>(INode<T> currentNode, Stack<IContinuation<T>> continuations)
    {
        if (Execution.Planning.Resource.ResourcePlanner.TryCreate(currentNode, out var resourcePlan))
        {
            IDisposable resource;
            try { resource = resourcePlan.Acquire(); }
            catch (Exception ex) { return new ResourceResult<T>(true, null, Outcome.Failure<T>(ex)); }

            var disposeContinuation = resourcePlan.CreateDisposeCont(resource);
            try
            {
                var nextNode = resourcePlan.Use(resource);
                continuations.Push(disposeContinuation);
                return new ResourceResult<T>(true, nextNode, null);
            }
            catch (Exception ex)
            {
                continuations.Push(disposeContinuation);
                return new ResourceResult<T>(true, null, Outcome.Failure<T>(ex));
            }
        }
        return new ResourceResult<T>(false, null, null);
    }
}
