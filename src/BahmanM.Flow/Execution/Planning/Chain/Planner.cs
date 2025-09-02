using System.Reflection;
using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations.Chain;

namespace BahmanM.Flow.Execution.Planning.Chain;

internal static class ChainPlanner
{
    private static readonly Type ChainSyncDef = typeof(BahmanM.Flow.Ast.Chain.Sync<,>);
    private static readonly Type ChainAsyncDef = typeof(BahmanM.Flow.Ast.Chain.Async<,>);
    private static readonly Type ChainCancellableDef = typeof(BahmanM.Flow.Ast.Chain.CancellableAsync<,>);

    internal static bool TryPlan<TOut>(INode<TOut> node, out PlannedFrame<TOut> plan)
    {
        var t = node.GetType();
        if (!t.IsGenericType)
        {
            plan = null!;
            return false;
        }

        var def = t.GetGenericTypeDefinition();
        var args = t.GetGenericArguments();
        var tIn = args[0];

        if (def == ChainSyncDef)
        {
            plan = PlanChainSync<TOut>(node, tIn);
            return true;
        }
        if (def == ChainAsyncDef)
        {
            plan = PlanChainAsync<TOut>(node, tIn);
            return true;
        }
        if (def == ChainCancellableDef)
        {
            plan = PlanChainCancellable<TOut>(node, tIn);
            return true;
        }

        plan = null!;
        return false;
    }

    private static PlannedFrame<TOut> PlanChainSync<TOut>(object node, Type tIn)
    {
        var method = typeof(ChainPlanner).GetMethod(nameof(PlanChainSyncGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var gmethod = method.MakeGenericMethod(tIn, typeof(TOut));
        return (PlannedFrame<TOut>)gmethod.Invoke(null, [node])!;
    }

    private static PlannedFrame<TOut> PlanChainAsync<TOut>(object node, Type tIn)
    {
        var method = typeof(ChainPlanner).GetMethod(nameof(PlanChainAsyncGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var gmethod = method.MakeGenericMethod(tIn, typeof(TOut));
        return (PlannedFrame<TOut>)gmethod.Invoke(null, [node])!;
    }

    private static PlannedFrame<TOut> PlanChainCancellable<TOut>(object node, Type tIn)
    {
        var method = typeof(ChainPlanner).GetMethod(nameof(PlanChainCancellableGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var gmethod = method.MakeGenericMethod(tIn, typeof(TOut));
        return (PlannedFrame<TOut>)gmethod.Invoke(null, [node])!;
    }

    private static PlannedFrame<TOut> PlanChainSyncGeneric<TIn, TOut>(BahmanM.Flow.Ast.Chain.Sync<TIn, TOut> n)
    {
        var cont = new ChainCont<TIn, TOut>(n.Operation);
        var upstream = n.Upstream.AsNode();
        if (typeof(TIn) == typeof(TOut))
        {
            return new PlannedFrame<TOut>((INode<TOut>)upstream, null, cont);
        }
        var eval = PlannerCommon.CreateEvaluateUpstream<TIn>(upstream);
        return new PlannedFrame<TOut>(null, eval, cont);
    }

    private static PlannedFrame<TOut> PlanChainAsyncGeneric<TIn, TOut>(BahmanM.Flow.Ast.Chain.Async<TIn, TOut> n)
    {
        var cont = new ChainAsyncCont<TIn, TOut>(n.Operation);
        var upstream = n.Upstream.AsNode();
        if (typeof(TIn) == typeof(TOut))
        {
            return new PlannedFrame<TOut>((INode<TOut>)upstream, null, cont);
        }
        var eval = PlannerCommon.CreateEvaluateUpstream<TIn>(upstream);
        return new PlannedFrame<TOut>(null, eval, cont);
    }

    private static PlannedFrame<TOut> PlanChainCancellableGeneric<TIn, TOut>(BahmanM.Flow.Ast.Chain.CancellableAsync<TIn, TOut> n)
    {
        var cont = new ChainCancellableCont<TIn, TOut>(n.Operation);
        var upstream = n.Upstream.AsNode();
        if (typeof(TIn) == typeof(TOut))
        {
            return new PlannedFrame<TOut>((INode<TOut>)upstream, null, cont);
        }
        var eval = PlannerCommon.CreateEvaluateUpstream<TIn>(upstream);
        return new PlannedFrame<TOut>(null, eval, cont);
    }
}
