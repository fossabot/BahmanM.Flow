using System.Reflection;
using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations.DoOnFailure;

namespace BahmanM.Flow.Execution.Planning.DoOnFailure;

internal static class DoOnFailurePlanner
{
    private static readonly Type SyncDef = typeof(BahmanM.Flow.Ast.DoOnFailure.Sync<>);
    private static readonly Type AsyncDef = typeof(BahmanM.Flow.Ast.DoOnFailure.Async<>);
    private static readonly Type CancellableDef = typeof(BahmanM.Flow.Ast.DoOnFailure.CancellableAsync<>);

    internal static bool TryPlan<TOut>(INode<TOut> node, out PlannedFrame<TOut> plan)
    {
        var t = node.GetType();
        if (!t.IsGenericType)
        {
            plan = null!;
            return false;
        }

        var def = t.GetGenericTypeDefinition();
        var method = def switch
        {
            var d when d == SyncDef => typeof(DoOnFailurePlanner).GetMethod(nameof(PlanSyncGeneric), BindingFlags.NonPublic | BindingFlags.Static),
            var d when d == AsyncDef => typeof(DoOnFailurePlanner).GetMethod(nameof(PlanAsyncGeneric), BindingFlags.NonPublic | BindingFlags.Static),
            var d when d == CancellableDef => typeof(DoOnFailurePlanner).GetMethod(nameof(PlanCancellableGeneric), BindingFlags.NonPublic | BindingFlags.Static),
            _ => null
        };

        if (method is null)
        {
            plan = null!;
            return false;
        }

        var gmethod = method.MakeGenericMethod(typeof(TOut));
        plan = (PlannedFrame<TOut>)gmethod.Invoke(null, [node])!;
        return true;
    }

    private static PlannedFrame<T> PlanSyncGeneric<T>(BahmanM.Flow.Ast.DoOnFailure.Sync<T> n)
        => new(n.Upstream.AsNode(), null, new DoOnFailureCont<T>(n.Action));

    private static PlannedFrame<T> PlanAsyncGeneric<T>(BahmanM.Flow.Ast.DoOnFailure.Async<T> n)
        => new(n.Upstream.AsNode(), null, new DoOnFailureAsyncCont<T>(n.AsyncAction));

    private static PlannedFrame<T> PlanCancellableGeneric<T>(BahmanM.Flow.Ast.DoOnFailure.CancellableAsync<T> n)
        => new(n.Upstream.AsNode(), null, new DoOnFailureCancellableCont<T>(n.AsyncAction));
}
