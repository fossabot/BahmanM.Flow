using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Engine.Concurrency;
using BahmanM.Flow.Execution.Engine.Planning;
using BahmanM.Flow.Execution.Engine.Primitives;
using BahmanM.Flow.Execution.Engine.Resource;
using BahmanM.Flow.Execution.Engine.Unwind;
using BahmanM.Flow.Execution.Engine.Operators;

namespace BahmanM.Flow.Execution.Engine;

internal static class Interpreter
{
    internal static async Task<Outcome<T>> ExecuteAsync<T>(INode<T> rootNode, Options executionOptions)
    {
        var interpreterState = new InterpreterState<T>(rootNode, executionOptions);
        IUnwinder<T> unwinder = new ContinuationUnwinderAdapter<T>();

        while (HasPendingWork(interpreterState))
        {
            if (HasPendingNode(interpreterState))
            {
                await DescendOneStepOrThrowAsync(interpreterState);
            }
            else
            {
                await UnwindOneStepAsync(interpreterState, unwinder);
            }
        }

        return (Outcome<T>)interpreterState.CurrentOutcome!;
    }

    private static async Task<bool> TryHandleConcurrencyAsync<T>(InterpreterState<T> interpreterState, INode<T> nodeUnderEvaluation)
    {
        var effect = await ConcurrencyHandler
            .TryHandleAsync(nodeUnderEvaluation, interpreterState.Options);
        if (effect.Kind == DescendEffectKind.NotHandled)
            return false;
        EngineEffects.Apply(interpreterState, effect);
        return true;
    }

    private static async Task<bool> TryHandlePlanningAsync<T>(InterpreterState<T> interpreterState, INode<T> nodeUnderEvaluation)
    {
        var effect = await PlanningHandler
            .TryHandleAsync(nodeUnderEvaluation, interpreterState.Continuations, interpreterState.Options);
        if (effect.Kind == DescendEffectKind.NotHandled)
            return false;
        EngineEffects.Apply(interpreterState, effect);
        return true;
    }

    private static bool TryHandleResource<T>(InterpreterState<T> interpreterState, INode<T> nodeUnderEvaluation)
    {
        var effect = ResourceHandler
            .TryHandle(nodeUnderEvaluation, interpreterState.Continuations);
        if (effect.Kind == DescendEffectKind.NotHandled)
            return false;
        EngineEffects.Apply(interpreterState, effect);
        return true;
    }

    private static async Task<bool> TryHandlePrimitiveAsync<T>(InterpreterState<T> interpreterState, INode<T> nodeUnderEvaluation)
    {
        var effect = await PrimitiveHandler
            .TryHandleAsync(nodeUnderEvaluation, interpreterState.Options);
        if (effect.Kind == DescendEffectKind.NotHandled)
            return false;
        EngineEffects.Apply(interpreterState, effect);
        return true;
    }

    private static bool TryHandleOperator<T>(InterpreterState<T> interpreterState, INode<T> nodeUnderEvaluation)
    {
        var effect = OperatorHandler
            .TryCreateContinuation(nodeUnderEvaluation);
        if (effect.Kind == DescendEffectKind.NotHandled)
            return false;
        EngineEffects.Apply(interpreterState, effect);
        return true;
    }

    private static async Task UnwindOneStepAsync<T>(InterpreterState<T> interpreterState, IUnwinder<T> unwinder)
    {
        var unwindResult = await unwinder.UnwindAsync(interpreterState);
        if (unwindResult.NextNode is not null)
        {
            interpreterState.CurrentNode = unwindResult.NextNode;
            interpreterState.CurrentOutcome = null;
            return;
        }
        interpreterState.CurrentOutcome = unwindResult.FinalOutcome;
    }

    private static bool HasPendingWork<T>(InterpreterState<T> interpreterState) =>
        interpreterState.CurrentNode is not null || interpreterState.Continuations.Count > 0;

    private static bool HasPendingNode<T>(InterpreterState<T> interpreterState) =>
        interpreterState.CurrentNode is not null;

    private static async Task DescendOneStepOrThrowAsync<T>(InterpreterState<T> interpreterState)
    {
        var nodeUnderEvaluation = interpreterState.CurrentNode ?? throw new InvalidOperationException("Descend requested with no current node.");

        if (await TryHandleConcurrencyAsync(interpreterState, nodeUnderEvaluation))
            return;
        if (await TryHandlePlanningAsync(interpreterState, nodeUnderEvaluation))
            return;
        if (TryHandleResource(interpreterState, nodeUnderEvaluation))
            return;
        if (await TryHandlePrimitiveAsync(interpreterState, nodeUnderEvaluation))
            return;
        if (TryHandleOperator(interpreterState, nodeUnderEvaluation))
            return;

        throw new NotSupportedException($"Unsupported node type: {nodeUnderEvaluation.GetType().FullName}");
    }
}
