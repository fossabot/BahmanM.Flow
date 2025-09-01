using System.Reflection;
using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;
using BahmanM.Flow.Execution.Continuations.Chain;
using BahmanM.Flow.Execution.Continuations.Select;
using BahmanM.Flow.Execution.Engine;

namespace BahmanM.Flow.Execution.Planning;

internal static class NodePlanner
{
    internal sealed record PlannedFrame<TOut>(Func<Options, Task<object>> EvaluateUpstream, IContinuation<TOut> Continuation);

    private static readonly Type SelectSyncDef = typeof(BahmanM.Flow.Ast.Select.Sync<,>);
    private static readonly Type SelectAsyncDef = typeof(BahmanM.Flow.Ast.Select.Async<,>);
    private static readonly Type SelectCancellableDef = typeof(BahmanM.Flow.Ast.Select.CancellableAsync<,>);
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

        if (def == SelectSyncDef)
        {
            plan = PlanSelectSync<TOut>(node, tIn);
            return true;
        }
        if (def == SelectAsyncDef)
        {
            plan = PlanSelectAsync<TOut>(node, tIn);
            return true;
        }
        if (def == SelectCancellableDef)
        {
            plan = PlanSelectCancellable<TOut>(node, tIn);
            return true;
        }
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

    private static PlannedFrame<TOut> PlanSelectSync<TOut>(object node, Type tIn)
    {
        var method = typeof(NodePlanner).GetMethod(nameof(PlanSelectSyncGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var gmethod = method.MakeGenericMethod(tIn, typeof(TOut));
        return (PlannedFrame<TOut>)gmethod.Invoke(null, new object[] { node })!;
    }

    private static PlannedFrame<TOut> PlanSelectAsync<TOut>(object node, Type tIn)
    {
        var method = typeof(NodePlanner).GetMethod(nameof(PlanSelectAsyncGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var gmethod = method.MakeGenericMethod(tIn, typeof(TOut));
        return (PlannedFrame<TOut>)gmethod.Invoke(null, new object[] { node })!;
    }

    private static PlannedFrame<TOut> PlanSelectCancellable<TOut>(object node, Type tIn)
    {
        var method = typeof(NodePlanner).GetMethod(nameof(PlanSelectCancellableGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var gmethod = method.MakeGenericMethod(tIn, typeof(TOut));
        return (PlannedFrame<TOut>)gmethod.Invoke(null, new object[] { node })!;
    }

    private static PlannedFrame<TOut> PlanChainSync<TOut>(object node, Type tIn)
    {
        var method = typeof(NodePlanner).GetMethod(nameof(PlanChainSyncGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var gmethod = method.MakeGenericMethod(tIn, typeof(TOut));
        return (PlannedFrame<TOut>)gmethod.Invoke(null, new object[] { node })!;
    }

    private static PlannedFrame<TOut> PlanChainAsync<TOut>(object node, Type tIn)
    {
        var method = typeof(NodePlanner).GetMethod(nameof(PlanChainAsyncGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var gmethod = method.MakeGenericMethod(tIn, typeof(TOut));
        return (PlannedFrame<TOut>)gmethod.Invoke(null, new object[] { node })!;
    }

    private static PlannedFrame<TOut> PlanChainCancellable<TOut>(object node, Type tIn)
    {
        var method = typeof(NodePlanner).GetMethod(nameof(PlanChainCancellableGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var gmethod = method.MakeGenericMethod(tIn, typeof(TOut));
        return (PlannedFrame<TOut>)gmethod.Invoke(null, new object[] { node })!;
    }

    private static PlannedFrame<TOut> PlanSelectSyncGeneric<TIn, TOut>(BahmanM.Flow.Ast.Select.Sync<TIn, TOut> n)
    {
        var cont = new SelectCont<TIn, TOut>(n.Operation);
        var eval = CreateEvaluateUpstream<TIn>(n.Upstream.AsNode());
        return new PlannedFrame<TOut>(eval, cont);
    }

    private static PlannedFrame<TOut> PlanSelectAsyncGeneric<TIn, TOut>(BahmanM.Flow.Ast.Select.Async<TIn, TOut> n)
    {
        var cont = new SelectAsyncCont<TIn, TOut>(n.Operation);
        var eval = CreateEvaluateUpstream<TIn>(n.Upstream.AsNode());
        return new PlannedFrame<TOut>(eval, cont);
    }

    private static PlannedFrame<TOut> PlanSelectCancellableGeneric<TIn, TOut>(BahmanM.Flow.Ast.Select.CancellableAsync<TIn, TOut> n)
    {
        var cont = new SelectCancellableCont<TIn, TOut>(n.Operation);
        var eval = CreateEvaluateUpstream<TIn>(n.Upstream.AsNode());
        return new PlannedFrame<TOut>(eval, cont);
    }

    private static PlannedFrame<TOut> PlanChainSyncGeneric<TIn, TOut>(BahmanM.Flow.Ast.Chain.Sync<TIn, TOut> n)
    {
        var cont = new ChainCont<TIn, TOut>(n.Operation);
        var eval = CreateEvaluateUpstream<TIn>(n.Upstream.AsNode());
        return new PlannedFrame<TOut>(eval, cont);
    }

    private static PlannedFrame<TOut> PlanChainAsyncGeneric<TIn, TOut>(BahmanM.Flow.Ast.Chain.Async<TIn, TOut> n)
    {
        var cont = new ChainAsyncCont<TIn, TOut>(n.Operation);
        var eval = CreateEvaluateUpstream<TIn>(n.Upstream.AsNode());
        return new PlannedFrame<TOut>(eval, cont);
    }

    private static PlannedFrame<TOut> PlanChainCancellableGeneric<TIn, TOut>(BahmanM.Flow.Ast.Chain.CancellableAsync<TIn, TOut> n)
    {
        var cont = new ChainCancellableCont<TIn, TOut>(n.Operation);
        var eval = CreateEvaluateUpstream<TIn>(n.Upstream.AsNode());
        return new PlannedFrame<TOut>(eval, cont);
    }

    private static Func<Options, Task<object>> CreateEvaluateUpstream<TIn>(INode<TIn> upstream)
        => async (Options options) => await Interpreter.ExecuteAsync(upstream, options);
}
