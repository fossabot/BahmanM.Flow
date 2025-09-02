using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;

namespace BahmanM.Flow.Execution.Engine.Resource;

internal static class ResourceScope
{
    internal static ResourceResult<T> TryOpen<T>(INode<T> node, Stack<IContinuation<T>> conts)
    {
        if (Execution.Planning.Resource.ResourcePlanner.TryCreate(node, out var wrPlan))
        {
            IDisposable resource;
            try { resource = wrPlan.Acquire(); }
            catch (Exception ex) { return new ResourceResult<T>(true, null, Outcome.Failure<T>(ex)); }

            var disposeCont = wrPlan.CreateDisposeCont(resource);
            try
            {
                var next = wrPlan.Use(resource);
                conts.Push(disposeCont);
                return new ResourceResult<T>(true, next, null);
            }
            catch (Exception ex)
            {
                conts.Push(disposeCont);
                return new ResourceResult<T>(true, null, Outcome.Failure<T>(ex));
            }
        }
        return new ResourceResult<T>(false, null, null);
    }
}

