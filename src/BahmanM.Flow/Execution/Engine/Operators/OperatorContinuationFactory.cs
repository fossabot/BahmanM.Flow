using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;
using BahmanM.Flow.Execution.Continuations.DoOnFailure;
using BahmanM.Flow.Execution.Continuations.DoOnSuccess;
using BahmanM.Flow.Execution.Continuations.Recover;
using BahmanM.Flow.Execution.Continuations.Validate;

namespace BahmanM.Flow.Execution.Engine.Operators;

internal static class OperatorContinuationFactory
{
    internal static bool TryPush<T>(ref INode<T> currentNode, Stack<IContinuation<T>> continuations)
    {
        switch (currentNode)
        {
            case Ast.DoOnSuccess.Sync<T> successSync:
                continuations.Push(new DoOnSuccessCont<T>(successSync.Action));
                currentNode = (INode<T>)successSync.Upstream; return true;
            case Ast.DoOnSuccess.Async<T> successAsync:
                continuations.Push(new DoOnSuccessAsyncCont<T>(successAsync.AsyncAction));
                currentNode = (INode<T>)successAsync.Upstream; return true;
            case Ast.DoOnSuccess.CancellableAsync<T> successCancellable:
                continuations.Push(new DoOnSuccessCancellableCont<T>(successCancellable.AsyncAction));
                currentNode = (INode<T>)successCancellable.Upstream; return true;
            case Ast.DoOnFailure.Sync<T> failureSync:
                continuations.Push(new DoOnFailureCont<T>(failureSync.Action));
                currentNode = (INode<T>)failureSync.Upstream; return true;
            case Ast.DoOnFailure.Async<T> failureAsync:
                continuations.Push(new DoOnFailureAsyncCont<T>(failureAsync.AsyncAction));
                currentNode = (INode<T>)failureAsync.Upstream; return true;
            case Ast.DoOnFailure.CancellableAsync<T> failureCancellable:
                continuations.Push(new DoOnFailureCancellableCont<T>(failureCancellable.AsyncAction));
                currentNode = (INode<T>)failureCancellable.Upstream; return true;
            case Ast.Validate.Sync<T> validateSync:
                continuations.Push(new ValidateCont<T>(validateSync.Predicate, validateSync.ExceptionFactory));
                currentNode = (INode<T>)validateSync.Upstream; return true;
            case Ast.Validate.Async<T> validateAsync:
                continuations.Push(new ValidateAsyncCont<T>(validateAsync.PredicateAsync, validateAsync.ExceptionFactory));
                currentNode = (INode<T>)validateAsync.Upstream; return true;
            case Ast.Validate.CancellableAsync<T> validateCancellable:
                continuations.Push(new ValidateCancellableCont<T>(validateCancellable.PredicateCancellableAsync, validateCancellable.ExceptionFactory));
                currentNode = (INode<T>)validateCancellable.Upstream; return true;
            case Ast.Recover.Sync<T> recoverSync:
                continuations.Push(new RecoverCont<T>(recoverSync.Recover));
                currentNode = (INode<T>)recoverSync.Source; return true;
            case Ast.Recover.Async<T> recoverAsync:
                continuations.Push(new RecoverAsyncCont<T>(recoverAsync.Recover));
                currentNode = (INode<T>)recoverAsync.Source; return true;
            case Ast.Recover.CancellableAsync<T> recoverCancellable:
                continuations.Push(new RecoverCancellableCont<T>((ex, ct) => recoverCancellable.Recover(ex, ct)));
                currentNode = (INode<T>)recoverCancellable.Source; return true;
        }
        return false;
    }
}
