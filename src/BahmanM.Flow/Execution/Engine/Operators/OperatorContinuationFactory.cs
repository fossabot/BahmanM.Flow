using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;
using BahmanM.Flow.Execution.Continuations.DoOnFailure;
using BahmanM.Flow.Execution.Continuations.DoOnSuccess;
using BahmanM.Flow.Execution.Continuations.Recover;
using BahmanM.Flow.Execution.Continuations.Validate;

namespace BahmanM.Flow.Execution.Engine.Operators;

internal static class OperatorContinuationFactory
{
    internal static bool TryPush<T>(ref INode<T> node, Stack<IContinuation<T>> conts)
    {
        switch (node)
        {
            case Ast.DoOnSuccess.Sync<T> dss:
                conts.Push(new DoOnSuccessCont<T>(dss.Action));
                node = (INode<T>)dss.Upstream; return true;
            case Ast.DoOnSuccess.Async<T> dsa:
                conts.Push(new DoOnSuccessAsyncCont<T>(dsa.AsyncAction));
                node = (INode<T>)dsa.Upstream; return true;
            case Ast.DoOnSuccess.CancellableAsync<T> dsc:
                conts.Push(new DoOnSuccessCancellableCont<T>(dsc.AsyncAction));
                node = (INode<T>)dsc.Upstream; return true;
            case Ast.DoOnFailure.Sync<T> dfs:
                conts.Push(new DoOnFailureCont<T>(dfs.Action));
                node = (INode<T>)dfs.Upstream; return true;
            case Ast.DoOnFailure.Async<T> dfa:
                conts.Push(new DoOnFailureAsyncCont<T>(dfa.AsyncAction));
                node = (INode<T>)dfa.Upstream; return true;
            case Ast.DoOnFailure.CancellableAsync<T> dfc:
                conts.Push(new DoOnFailureCancellableCont<T>(dfc.AsyncAction));
                node = (INode<T>)dfc.Upstream; return true;
            case Ast.Validate.Sync<T> vs:
                conts.Push(new ValidateCont<T>(vs.Predicate, vs.ExceptionFactory));
                node = (INode<T>)vs.Upstream; return true;
            case Ast.Validate.Async<T> va:
                conts.Push(new ValidateAsyncCont<T>(va.PredicateAsync, va.ExceptionFactory));
                node = (INode<T>)va.Upstream; return true;
            case Ast.Validate.CancellableAsync<T> vc:
                conts.Push(new ValidateCancellableCont<T>(vc.PredicateCancellableAsync, vc.ExceptionFactory));
                node = (INode<T>)vc.Upstream; return true;
            case Ast.Recover.Sync<T> rs:
                conts.Push(new RecoverCont<T>(rs.Recover));
                node = (INode<T>)rs.Source; return true;
            case Ast.Recover.Async<T> ra:
                conts.Push(new RecoverAsyncCont<T>(ra.Recover));
                node = (INode<T>)ra.Source; return true;
            case Ast.Recover.CancellableAsync<T> rc:
                conts.Push(new RecoverCancellableCont<T>((ex, ct) => rc.Recover(ex, ct)));
                node = (INode<T>)rc.Source; return true;
        }
        return false;
    }
}

