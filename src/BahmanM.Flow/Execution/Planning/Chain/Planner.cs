using System.Reflection;
using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations.Chain;

namespace BahmanM.Flow.Execution.Planning.Chain;

internal static class ChainPlanner
{
    private static readonly Type ChainSyncDef = typeof(BahmanM.Flow.Ast.Chain.Sync<,>);
    private static readonly Type ChainAsyncDef = typeof(BahmanM.Flow.Ast.Chain.Async<,>);
    private static readonly Type ChainCancellableDef = typeof(BahmanM.Flow.Ast.Chain.CancellableAsync<,>);

    internal static bool TryPlan<TOut>(INode<TOut> node, out PlannedFrame<TOut> plannedFrame)
    {
        var nodeType = node.GetType();
        if (!nodeType.IsGenericType)
        {
            plannedFrame = null!;
            return false;
        }

        var genericDefinition = nodeType.GetGenericTypeDefinition();
        var typeArguments = nodeType.GetGenericArguments();
        var inputType = typeArguments[0];

        if (genericDefinition == ChainSyncDef)
        {
            plannedFrame = PlanChainSync<TOut>(node, inputType);
            return true;
        }
        if (genericDefinition == ChainAsyncDef)
        {
            plannedFrame = PlanChainAsync<TOut>(node, inputType);
            return true;
        }
        if (genericDefinition == ChainCancellableDef)
        {
            plannedFrame = PlanChainCancellable<TOut>(node, inputType);
            return true;
        }

        plannedFrame = null!;
        return false;
    }

    private static PlannedFrame<TOut> PlanChainSync<TOut>(object node, Type inputType)
    {
        var dispatch = typeof(ChainPlanner).GetMethod(nameof(PlanChainSyncGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var genericMethod = dispatch.MakeGenericMethod(inputType, typeof(TOut));
        return (PlannedFrame<TOut>)genericMethod.Invoke(null, [node])!;
    }

    private static PlannedFrame<TOut> PlanChainAsync<TOut>(object node, Type inputType)
    {
        var dispatch = typeof(ChainPlanner).GetMethod(nameof(PlanChainAsyncGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var genericMethod = dispatch.MakeGenericMethod(inputType, typeof(TOut));
        return (PlannedFrame<TOut>)genericMethod.Invoke(null, [node])!;
    }

    private static PlannedFrame<TOut> PlanChainCancellable<TOut>(object node, Type inputType)
    {
        var dispatch = typeof(ChainPlanner).GetMethod(nameof(PlanChainCancellableGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var genericMethod = dispatch.MakeGenericMethod(inputType, typeof(TOut));
        return (PlannedFrame<TOut>)genericMethod.Invoke(null, [node])!;
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
