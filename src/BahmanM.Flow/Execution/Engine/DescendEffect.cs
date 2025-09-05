namespace BahmanM.Flow.Execution.Engine;

internal enum DescendEffectKind
{
    NotHandled,
    SetOutcome,
    SetNextNode,
    PushContinuation
}

internal readonly record struct DescendEffect<T>(
    DescendEffectKind Kind,
    object? Outcome = null,
    Ast.INode<T>? NextNode = null,
    Execution.Continuations.IContinuation<T>? Continuation = null,
    Ast.INode<T>? UpstreamForPush = null);
