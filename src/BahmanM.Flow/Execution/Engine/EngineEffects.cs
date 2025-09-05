using BahmanM.Flow.Ast;

namespace BahmanM.Flow.Execution.Engine;

internal static class EngineEffects
{
    internal static void Apply<T>(InterpreterState<T> state, DescendEffect<T> effect)
    {
        switch (effect.Kind)
        {
            case DescendEffectKind.SetOutcome:
                state.CurrentOutcome = effect.Outcome;
                state.CurrentNode = null;
                break;
            case DescendEffectKind.SetNextNode:
                state.CurrentNode = effect.NextNode;
                state.CurrentOutcome = null;
                break;
            case DescendEffectKind.PushContinuation:
                state.Continuations.Push(effect.Continuation!);
                state.CurrentNode = effect.UpstreamForPush;
                state.CurrentOutcome = null;
                break;
            case DescendEffectKind.NotHandled:
            default:
                break;
        }
    }
}
