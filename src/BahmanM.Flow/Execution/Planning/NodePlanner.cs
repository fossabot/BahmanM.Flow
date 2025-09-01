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
        return PlanSelectFusionGeneric(n.Upstream, initialSync: n.Operation, initialAsync: null, initialCanc: null);
    }

    private static PlannedFrame<TOut> PlanSelectAsyncGeneric<TIn, TOut>(BahmanM.Flow.Ast.Select.Async<TIn, TOut> n)
    {
        return PlanSelectFusionGeneric(n.Upstream, initialSync: null, initialAsync: n.Operation, initialCanc: null);
    }

    private static PlannedFrame<TOut> PlanSelectCancellableGeneric<TIn, TOut>(BahmanM.Flow.Ast.Select.CancellableAsync<TIn, TOut> n)
    {
        return PlanSelectFusionGeneric(n.Upstream, initialSync: null, initialAsync: null, initialCanc: n.Operation);
    }

    private static PlannedFrame<TOut> PlanSelectFusionGeneric<TIn, TOut>(IFlow<TIn> upstreamStart,
        Flow.Operations.Select.Sync<TIn, TOut>? initialSync,
        Flow.Operations.Select.Async<TIn, TOut>? initialAsync,
        Flow.Operations.Select.CancellableAsync<TIn, TOut>? initialCanc)
    {
        // Fast path: only fuse same-type selects (TIn == TOut). Otherwise fallback to simple planning.
        if (typeof(TIn) != typeof(TOut))
        {
            if (initialSync is not null)
                return new PlannedFrame<TOut>(CreateEvaluateUpstream<TIn>(upstreamStart.AsNode()), new SelectCont<TIn, TOut>(initialSync));
            if (initialAsync is not null)
                return new PlannedFrame<TOut>(CreateEvaluateUpstream<TIn>(upstreamStart.AsNode()), new SelectAsyncCont<TIn, TOut>(initialAsync));
            // initialCanc not null
            return new PlannedFrame<TOut>(CreateEvaluateUpstream<TIn>(upstreamStart.AsNode()), new SelectCancellableCont<TIn, TOut>(initialCanc!));
        }

        // Fuse chain of Select< TIn, TIn > variants
        var opsSync = new List<Flow.Operations.Select.Sync<TIn, TIn>>();
        var opsAsync = new List<Flow.Operations.Select.Async<TIn, TIn>>();
        var opsCanc = new List<Flow.Operations.Select.CancellableAsync<TIn, TIn>>();

        // Seed with the initial op
        if (initialSync is not null)
            opsSync.Add(x => (TIn)(object)initialSync((TIn)(object)x)!);
        if (initialAsync is not null)
            opsAsync.Add(async x => (TIn)(object)await initialAsync((TIn)(object)x)!);
        if (initialCanc is not null)
            opsCanc.Add(async (x, ct) => (TIn)(object)await initialCanc((TIn)(object)x, ct)!);

        var upstream = upstreamStart;
        while (true)
        {
            switch (upstream)
            {
                case BahmanM.Flow.Ast.Select.Sync<TIn, TIn> ss:
                    opsSync.Add(ss.Operation);
                    upstream = ss.Upstream;
                    continue;
                case BahmanM.Flow.Ast.Select.Async<TIn, TIn> sa:
                    opsAsync.Add(sa.Operation);
                    upstream = sa.Upstream;
                    continue;
                case BahmanM.Flow.Ast.Select.CancellableAsync<TIn, TIn> sc:
                    opsCanc.Add(sc.Operation);
                    upstream = sc.Upstream;
                    continue;
                default:
                    break;
            }
            break;
        }

        // Build fused continuation
        if (opsCanc.Count > 0)
        {
            async Task<TIn> Fused(TIn v, CancellationToken ct)
            {
                var acc = v;
                foreach (var op in opsSync) acc = op(acc);
                foreach (var op in opsAsync) acc = await op(acc);
                foreach (var op in opsCanc) acc = await op(acc, ct);
                return acc;
            }
            return new PlannedFrame<TOut>(CreateEvaluateUpstream<TIn>(upstream.AsNode()), new SelectCancellableCont<TIn, TOut>(async (input, ct) => (TOut)(object)await Fused(input, ct)));
        }
        if (opsAsync.Count > 0)
        {
            async Task<TIn> Fused(TIn v)
            {
                var acc = v;
                foreach (var op in opsSync) acc = op(acc);
                foreach (var op in opsAsync) acc = await op(acc);
                return acc;
            }
            return new PlannedFrame<TOut>(CreateEvaluateUpstream<TIn>(upstream.AsNode()), new SelectAsyncCont<TIn, TOut>(async input => (TOut)(object)await Fused(input)));
        }
        // All sync
        TIn FusedSync(TIn v)
        {
            var acc = v;
            foreach (var op in opsSync) acc = op(acc);
            return acc;
        }
        TOut FusedSyncOut(TIn v) => (TOut)(object)FusedSync(v);
        return new PlannedFrame<TOut>(CreateEvaluateUpstream<TIn>(upstream.AsNode()), new SelectCont<TIn, TOut>(FusedSyncOut));
    }

    private static PlannedFrame<TOut> PlanChainSyncGeneric<TIn, TOut>(BahmanM.Flow.Ast.Chain.Sync<TIn, TOut> n)
    {
        return PlanChainFusionGeneric(n.Upstream, initialSync: n.Operation, initialAsync: null, initialCanc: null);
    }

    private static PlannedFrame<TOut> PlanChainAsyncGeneric<TIn, TOut>(BahmanM.Flow.Ast.Chain.Async<TIn, TOut> n)
    {
        return PlanChainFusionGeneric(n.Upstream, initialSync: null, initialAsync: n.Operation, initialCanc: null);
    }

    private static PlannedFrame<TOut> PlanChainCancellableGeneric<TIn, TOut>(BahmanM.Flow.Ast.Chain.CancellableAsync<TIn, TOut> n)
    {
        return PlanChainFusionGeneric(n.Upstream, initialSync: null, initialAsync: null, initialCanc: n.Operation);
    }

    private static PlannedFrame<TOut> PlanChainFusionGeneric<TIn, TOut>(IFlow<TIn> upstreamStart,
        Flow.Operations.Chain.Sync<TIn, TOut>? initialSync,
        Flow.Operations.Chain.Async<TIn, TOut>? initialAsync,
        Flow.Operations.Chain.CancellableAsync<TIn, TOut>? initialCanc)
    {
        // Only fuse when TIn == TOut to keep continuation type simple; else fallback
        var sameType = typeof(TIn) == typeof(TOut);
        if (!sameType)
        {
            if (initialSync is not null)
                return new PlannedFrame<TOut>(CreateEvaluateUpstream<TIn>(upstreamStart.AsNode()), new ChainCont<TIn, TOut>(initialSync));
            if (initialAsync is not null)
                return new PlannedFrame<TOut>(CreateEvaluateUpstream<TIn>(upstreamStart.AsNode()), new ChainAsyncCont<TIn, TOut>(initialAsync));
            return new PlannedFrame<TOut>(CreateEvaluateUpstream<TIn>(upstreamStart.AsNode()), new ChainCancellableCont<TIn, TOut>(initialCanc!));
        }

        // Collect Chain<TIn,TIn> operations
        var opsSync = new List<Flow.Operations.Chain.Sync<TIn, TIn>>();
        var opsAsync = new List<Flow.Operations.Chain.Async<TIn, TIn>>();
        var opsCanc = new List<Flow.Operations.Chain.CancellableAsync<TIn, TIn>>();

        if (initialSync is not null)
            opsSync.Add(x => (IFlow<TIn>)(object)initialSync((TIn)(object)x)!);
        if (initialAsync is not null)
            opsAsync.Add(async x => (IFlow<TIn>)(object)await initialAsync((TIn)(object)x)!);
        if (initialCanc is not null)
            opsCanc.Add(async (x, ct) => (IFlow<TIn>)(object)await initialCanc((TIn)(object)x, ct)!);

        var upstream = upstreamStart;
        while (true)
        {
            switch (upstream)
            {
                case BahmanM.Flow.Ast.Chain.Sync<TIn, TIn> cs:
                    opsSync.Add(cs.Operation);
                    upstream = cs.Upstream;
                    continue;
                case BahmanM.Flow.Ast.Chain.Async<TIn, TIn> ca:
                    opsAsync.Add(ca.Operation);
                    upstream = ca.Upstream;
                    continue;
                case BahmanM.Flow.Ast.Chain.CancellableAsync<TIn, TIn> cc:
                    opsCanc.Add(cc.Operation);
                    upstream = cc.Upstream;
                    continue;
                default:
                    break;
            }
            break;
        }

        // Build fused sync delegate that returns IFlow<TIn> by chaining through Flow's Chain operators
        IFlow<TIn> FusedFlow(TIn v)
        {
            // Start with first op if any, otherwise identity succeed
            IFlow<TIn> flow = opsSync.Count > 0 ? opsSync[0](v) : Flow.Succeed(v);

            for (int i = (opsSync.Count > 0 ? 1 : 0); i < opsSync.Count; i++)
            {
                var op = opsSync[i];
                flow = flow.Chain(op);
            }
            foreach (var op in opsAsync)
            {
                flow = flow.Chain(op);
            }
            foreach (var op in opsCanc)
            {
                flow = flow.Chain(op);
            }
            return flow;
        }

        // We can always return a sync Chain continuation in same-type case
        Flow.Operations.Chain.Sync<TIn, TOut> fused = x => (IFlow<TOut>)(object)FusedFlow(x);
        return new PlannedFrame<TOut>(CreateEvaluateUpstream<TIn>(upstream.AsNode()), new ChainCont<TIn, TOut>(fused));
    }

    private static Func<Options, Task<object>> CreateEvaluateUpstream<TIn>(INode<TIn> upstream)
        => async (Options options) => await Interpreter.ExecuteAsync(upstream, options);
}
