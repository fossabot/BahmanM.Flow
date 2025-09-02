using System.Reflection;
using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations.Validate;

namespace BahmanM.Flow.Execution.Planning.Validate;

internal static class ValidatePlanner
{
    private static readonly Type ValidateSyncDef = typeof(BahmanM.Flow.Ast.Validate.Sync<>);
    private static readonly Type ValidateAsyncDef = typeof(BahmanM.Flow.Ast.Validate.Async<>);
    private static readonly Type ValidateCancellableDef = typeof(BahmanM.Flow.Ast.Validate.CancellableAsync<>);

    internal static bool TryPlan<TOut>(INode<TOut> node, out PlannedFrame<TOut> plannedFrame)
    {
        var nodeType = node.GetType();
        if (!nodeType.IsGenericType)
        {
            plannedFrame = null!;
            return false;
        }

        var genericDefinition = nodeType.GetGenericTypeDefinition();
        var dispatchMethod = genericDefinition switch
        {
            var d when d == ValidateSyncDef => typeof(ValidatePlanner).GetMethod(nameof(PlanSyncGeneric), BindingFlags.NonPublic | BindingFlags.Static),
            var d when d == ValidateAsyncDef => typeof(ValidatePlanner).GetMethod(nameof(PlanAsyncGeneric), BindingFlags.NonPublic | BindingFlags.Static),
            var d when d == ValidateCancellableDef => typeof(ValidatePlanner).GetMethod(nameof(PlanCancellableGeneric), BindingFlags.NonPublic | BindingFlags.Static),
            _ => null
        };

        if (dispatchMethod is null)
        {
            plannedFrame = null!;
            return false;
        }

        var genericMethod = dispatchMethod.MakeGenericMethod(typeof(TOut));
        plannedFrame = (PlannedFrame<TOut>)genericMethod.Invoke(null, [node])!;
        return true;
    }

    private static PlannedFrame<T> PlanSyncGeneric<T>(BahmanM.Flow.Ast.Validate.Sync<T> n)
        => new(n.Upstream.AsNode(), null, new ValidateCont<T>(n.Predicate, n.ExceptionFactory));

    private static PlannedFrame<T> PlanAsyncGeneric<T>(BahmanM.Flow.Ast.Validate.Async<T> n)
        => new(n.Upstream.AsNode(), null, new ValidateAsyncCont<T>(n.PredicateAsync, n.ExceptionFactory));

    private static PlannedFrame<T> PlanCancellableGeneric<T>(BahmanM.Flow.Ast.Validate.CancellableAsync<T> n)
        => new(n.Upstream.AsNode(), null, new ValidateCancellableCont<T>(n.PredicateCancellableAsync, n.ExceptionFactory));
}
