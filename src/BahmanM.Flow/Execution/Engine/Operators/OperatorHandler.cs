using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations.DoOnFailure;
using BahmanM.Flow.Execution.Continuations.DoOnSuccess;
using BahmanM.Flow.Execution.Continuations.Recover;
using BahmanM.Flow.Execution.Continuations.Validate;

namespace BahmanM.Flow.Execution.Engine.Operators;

internal static class OperatorHandler
{
    internal static DescendEffect<T> TryCreateContinuation<T>(INode<T> node)
    {
        switch (node)
        {
            case Ast.DoOnSuccess.Sync<T> successSync:
                return new DescendEffect<T>(DescendEffectKind.PushContinuation,
                    Continuation: new DoOnSuccessCont<T>(successSync.Action),
                    UpstreamForPush: (INode<T>)successSync.Upstream);
            case Ast.DoOnSuccess.Async<T> successAsync:
                return new DescendEffect<T>(DescendEffectKind.PushContinuation,
                    Continuation: new DoOnSuccessAsyncCont<T>(successAsync.AsyncAction),
                    UpstreamForPush: (INode<T>)successAsync.Upstream);
            case Ast.DoOnSuccess.CancellableAsync<T> successCancellable:
                return new DescendEffect<T>(DescendEffectKind.PushContinuation,
                    Continuation: new DoOnSuccessCancellableCont<T>(successCancellable.AsyncAction),
                    UpstreamForPush: (INode<T>)successCancellable.Upstream);
            case Ast.DoOnFailure.Sync<T> failureSync:
                return new DescendEffect<T>(DescendEffectKind.PushContinuation,
                    Continuation: new DoOnFailureCont<T>(failureSync.Action),
                    UpstreamForPush: (INode<T>)failureSync.Upstream);
            case Ast.DoOnFailure.Async<T> failureAsync:
                return new DescendEffect<T>(DescendEffectKind.PushContinuation,
                    Continuation: new DoOnFailureAsyncCont<T>(failureAsync.AsyncAction),
                    UpstreamForPush: (INode<T>)failureAsync.Upstream);
            case Ast.DoOnFailure.CancellableAsync<T> failureCancellable:
                return new DescendEffect<T>(DescendEffectKind.PushContinuation,
                    Continuation: new DoOnFailureCancellableCont<T>(failureCancellable.AsyncAction),
                    UpstreamForPush: (INode<T>)failureCancellable.Upstream);
            case Ast.Validate.Sync<T> validateSync:
                return new DescendEffect<T>(DescendEffectKind.PushContinuation,
                    Continuation: new ValidateCont<T>(validateSync.Predicate, validateSync.ExceptionFactory),
                    UpstreamForPush: (INode<T>)validateSync.Upstream);
            case Ast.Validate.Async<T> validateAsync:
                return new DescendEffect<T>(DescendEffectKind.PushContinuation,
                    Continuation: new ValidateAsyncCont<T>(validateAsync.PredicateAsync, validateAsync.ExceptionFactory),
                    UpstreamForPush: (INode<T>)validateAsync.Upstream);
            case Ast.Validate.CancellableAsync<T> validateCancellable:
                return new DescendEffect<T>(DescendEffectKind.PushContinuation,
                    Continuation: new ValidateCancellableCont<T>(validateCancellable.PredicateCancellableAsync, validateCancellable.ExceptionFactory),
                    UpstreamForPush: (INode<T>)validateCancellable.Upstream);
            case Ast.Recover.Sync<T> recoverSync:
                return new DescendEffect<T>(DescendEffectKind.PushContinuation,
                    Continuation: new RecoverCont<T>(recoverSync.Recover),
                    UpstreamForPush: (INode<T>)recoverSync.Source);
            case Ast.Recover.Async<T> recoverAsync:
                return new DescendEffect<T>(DescendEffectKind.PushContinuation,
                    Continuation: new RecoverAsyncCont<T>(recoverAsync.Recover),
                    UpstreamForPush: (INode<T>)recoverAsync.Source);
            case Ast.Recover.CancellableAsync<T> recoverCancellable:
                return new DescendEffect<T>(DescendEffectKind.PushContinuation,
                    Continuation: new RecoverCancellableCont<T>((ex, ct) => recoverCancellable.Recover(ex, ct)),
                    UpstreamForPush: (INode<T>)recoverCancellable.Source);
            default:
                return new DescendEffect<T>(DescendEffectKind.NotHandled);
        }
    }
}

