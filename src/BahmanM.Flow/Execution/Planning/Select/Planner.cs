using System.Reflection;
using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations.Select;

namespace BahmanM.Flow.Execution.Planning.Select;

internal static class SelectPlanner
{
    private static readonly Type SelectSyncDef = typeof(BahmanM.Flow.Ast.Select.Sync<,>);
    private static readonly Type SelectAsyncDef = typeof(BahmanM.Flow.Ast.Select.Async<,>);
    private static readonly Type SelectCancellableDef = typeof(BahmanM.Flow.Ast.Select.CancellableAsync<,>);

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

        if (genericDefinition == SelectSyncDef)
        {
            plannedFrame = PlanSelectSync<TOut>(node, inputType);
            return true;
        }
        if (genericDefinition == SelectAsyncDef)
        {
            plannedFrame = PlanSelectAsync<TOut>(node, inputType);
            return true;
        }
        if (genericDefinition == SelectCancellableDef)
        {
            plannedFrame = PlanSelectCancellable<TOut>(node, inputType);
            return true;
        }

        plannedFrame = null!;
        return false;
    }

    private static PlannedFrame<TOut> PlanSelectSync<TOut>(object node, Type inputType)
    {
        var dispatch = typeof(SelectPlanner).GetMethod(nameof(PlanSelectSyncGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var genericMethod = dispatch.MakeGenericMethod(inputType, typeof(TOut));
        return (PlannedFrame<TOut>)genericMethod.Invoke(null, [node])!;
    }

    private static PlannedFrame<TOut> PlanSelectAsync<TOut>(object node, Type inputType)
    {
        var dispatch = typeof(SelectPlanner).GetMethod(nameof(PlanSelectAsyncGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var genericMethod = dispatch.MakeGenericMethod(inputType, typeof(TOut));
        return (PlannedFrame<TOut>)genericMethod.Invoke(null, [node])!;
    }

    private static PlannedFrame<TOut> PlanSelectCancellable<TOut>(object node, Type inputType)
    {
        var dispatch = typeof(SelectPlanner).GetMethod(nameof(PlanSelectCancellableGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var genericMethod = dispatch.MakeGenericMethod(inputType, typeof(TOut));
        return (PlannedFrame<TOut>)genericMethod.Invoke(null, [node])!;
    }

    private static PlannedFrame<TOut> PlanSelectSyncGeneric<TIn, TOut>(BahmanM.Flow.Ast.Select.Sync<TIn, TOut> n)
        => PlanSelectFusionGeneric(n.Upstream, initialSync: n.Operation, initialAsync: null, initialCanc: null);

    private static PlannedFrame<TOut> PlanSelectAsyncGeneric<TIn, TOut>(BahmanM.Flow.Ast.Select.Async<TIn, TOut> n)
        => PlanSelectFusionGeneric(n.Upstream, initialSync: null, initialAsync: n.Operation, initialCanc: null);

    private static PlannedFrame<TOut> PlanSelectCancellableGeneric<TIn, TOut>(BahmanM.Flow.Ast.Select.CancellableAsync<TIn, TOut> n)
        => PlanSelectFusionGeneric(n.Upstream, initialSync: null, initialAsync: null, initialCanc: n.Operation);

    private static PlannedFrame<TOut> PlanSelectFusionGeneric<TIn, TOut>(IFlow<TIn> upstreamStart,
        Flow.Operations.Select.Sync<TIn, TOut>? initialSync,
        Flow.Operations.Select.Async<TIn, TOut>? initialAsync,
        Flow.Operations.Select.CancellableAsync<TIn, TOut>? initialCanc)
    {
        // Different type: no fusion; evaluate upstream and apply a single continuation.
        if (typeof(TIn) != typeof(TOut))
        {
            if (initialSync is not null)
                return new PlannedFrame<TOut>(null, PlannerCommon.CreateEvaluateUpstream<TIn>(upstreamStart.AsNode()), new SelectCont<TIn, TOut>(initialSync));
            if (initialAsync is not null)
                return new PlannedFrame<TOut>(null, PlannerCommon.CreateEvaluateUpstream<TIn>(upstreamStart.AsNode()), new SelectAsyncCont<TIn, TOut>(initialAsync));
            return new PlannedFrame<TOut>(null, PlannerCommon.CreateEvaluateUpstream<TIn>(upstreamStart.AsNode()), new SelectCancellableCont<TIn, TOut>(initialCanc!));
        }

        var dispatch = typeof(SelectPlanner).GetMethod(nameof(PlanSelectSameTypeGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var genericMethod = dispatch.MakeGenericMethod(typeof(TIn));
        return (PlannedFrame<TOut>)genericMethod.Invoke(null, [upstreamStart, initialSync, initialAsync, initialCanc])!;
    }

    private static PlannedFrame<T> PlanSelectSameTypeGeneric<T>(
        IFlow<T> upstreamStart,
        Flow.Operations.Select.Sync<T, T>? initialSync,
        Flow.Operations.Select.Async<T, T>? initialAsync,
        Flow.Operations.Select.CancellableAsync<T, T>? initialCanc)
    {
        var (upstreamNode, sequence) = MergeConsecutiveSelectOperations.MergeAdjacent(upstreamStart, initialSync, initialAsync, initialCanc);

        if (sequence.UsesCancellation)
        {
            return new PlannedFrame<T>(upstreamNode, null,
                new SelectCancellableCont<T, T>(async (value, ct) => await sequence.Run(value, ct)));
        }

        if (sequence.UsesAsync)
        {
            return new PlannedFrame<T>(upstreamNode, null,
                new SelectAsyncCont<T, T>(async value => await sequence.Run(value, CancellationToken.None)));
        }

        return new PlannedFrame<T>(upstreamNode, null,
            new SelectCont<T, T>(value => sequence.RunSync(value)));
    }
}
