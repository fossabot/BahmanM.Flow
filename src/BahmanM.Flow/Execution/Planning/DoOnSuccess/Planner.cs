using System.Reflection;
using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations.DoOnSuccess;

namespace BahmanM.Flow.Execution.Planning.DoOnSuccess;

internal static class DoOnSuccessPlanner
{
    private static readonly Type SyncDef = typeof(BahmanM.Flow.Ast.DoOnSuccess.Sync<>);
    private static readonly Type AsyncDef = typeof(BahmanM.Flow.Ast.DoOnSuccess.Async<>);
    private static readonly Type CancellableDef = typeof(BahmanM.Flow.Ast.DoOnSuccess.CancellableAsync<>);

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
            var d when d == SyncDef => typeof(DoOnSuccessPlanner).GetMethod(nameof(PlanSyncGeneric), BindingFlags.NonPublic | BindingFlags.Static),
            var d when d == AsyncDef => typeof(DoOnSuccessPlanner).GetMethod(nameof(PlanAsyncGeneric), BindingFlags.NonPublic | BindingFlags.Static),
            var d when d == CancellableDef => typeof(DoOnSuccessPlanner).GetMethod(nameof(PlanCancellableGeneric), BindingFlags.NonPublic | BindingFlags.Static),
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

    private static PlannedFrame<T> PlanSyncGeneric<T>(BahmanM.Flow.Ast.DoOnSuccess.Sync<T> n) =>
        new(n.Upstream.AsNode(), null, new DoOnSuccessCont<T>(n.Action));

    private static PlannedFrame<T> PlanAsyncGeneric<T>(BahmanM.Flow.Ast.DoOnSuccess.Async<T> n)
        => new(n.Upstream.AsNode(), null, new DoOnSuccessAsyncCont<T>(n.AsyncAction));

    private static PlannedFrame<T> PlanCancellableGeneric<T>(BahmanM.Flow.Ast.DoOnSuccess.CancellableAsync<T> n)
        => new(n.Upstream.AsNode(), null, new DoOnSuccessCancellableCont<T>(n.AsyncAction));
}
