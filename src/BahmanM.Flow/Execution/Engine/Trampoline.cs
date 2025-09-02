using System.Reflection;
using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;
using BahmanM.Flow.Execution.Continuations.DoOnFailure;
using BahmanM.Flow.Execution.Continuations.DoOnSuccess;
using BahmanM.Flow.Execution.Continuations.Recover;
using BahmanM.Flow.Execution.Continuations.Validate;
using BahmanM.Flow.Execution.Planning;

namespace BahmanM.Flow.Execution.Engine;

internal static class Interpreter
{
    internal static async Task<Outcome<T>> ExecuteAsync<T>(INode<T> root, Options options)
    {
        var conts = new Stack<IContinuation<T>>();
        INode<T>? node = root;
        object? outcome = null;

        while (true)
        {
            // Descend: push continuations; evaluate leaves
            while (node is not null)
            {
                // Handle composite nodes (All/Any) directly in trampoline
                if (IsAllNode(node))
                {
                    outcome = await EvaluateAllAsync(node, options);
                    node = null;
                    continue;
                }
                if (IsAnyNode(node))
                {
                    outcome = await EvaluateAnyAsync(node, options);
                    node = null;
                    continue;
                }

                // Plan Select/Chain as typed continuations with typed upstream evaluation
                if (NodePlanner.TryPlan(node, out var plan))
                {
                    conts.Push(plan.Continuation);
                    if (plan.UpstreamNode is not null)
                    {
                        node = plan.UpstreamNode;
                        continue;
                    }
                    if (plan.EvaluateUpstream is not null)
                    {
                        outcome = await plan.EvaluateUpstream(options);
                        node = null;
                        continue;
                    }
                }

                // Handle WithResource via planner with cached typed delegates
                if (Planning.Resource.ResourcePlanner.TryCreate(node, out var wrPlan))
                {
                    IDisposable resource;
                    try
                    {
                        resource = wrPlan.Acquire();
                    }
                    catch (Exception ex)
                    {
                        outcome = Outcome.Failure<T>(ex);
                        node = null;
                        continue;
                    }

                    var disposeCont = wrPlan.CreateDisposeCont(resource);
                    try
                    {
                        node = wrPlan.Use(resource);
                        conts.Push(disposeCont);
                    }
                    catch (Exception ex)
                    {
                        // If Use throws before we descend, return failure; disposal still occurs on unwind now that cont is present
                        outcome = Outcome.Failure<T>(ex);
                        node = null;
                        conts.Push(disposeCont);
                    }
                    continue;
                }

                switch (node)
                {
                    // Leaves
                    case Ast.Primitive.Succeed<T> s:
                        outcome = Outcome.Success(s.Value);
                        node = null;
                        break;

                    case Ast.Primitive.Fail<T> f:
                        outcome = Outcome.Failure<T>(f.Exception);
                        node = null;
                        break;

                    case Ast.Create.Sync<T> cSync:
                        outcome = await TryOperation.Sync<T>(() => cSync.Operation());
                        node = null;
                        break;

                    case Ast.Create.Async<T> cAsync:
                        outcome = await TryOperation.Async<T>(() => cAsync.Operation());
                        node = null;
                        break;

                    case Ast.Create.CancellableAsync<T> cCan:
                        outcome = await TryOperation.CancellableAsync<T>(ct => cCan.Operation(ct), options.CancellationToken);
                        node = null;
                        break;

                    // Side effects (no type change)
                    case Ast.DoOnSuccess.Sync<T> dss:
                        conts.Push(new DoOnSuccessCont<T>(dss.Action));
                        node = (INode<T>)dss.Upstream;
                        break;

                    case Ast.DoOnSuccess.Async<T> dsa:
                        conts.Push(new DoOnSuccessAsyncCont<T>(dsa.AsyncAction));
                        node = (INode<T>)dsa.Upstream;
                        break;

                    case Ast.DoOnSuccess.CancellableAsync<T> dsc:
                        conts.Push(new DoOnSuccessCancellableCont<T>(dsc.AsyncAction));
                        node = (INode<T>)dsc.Upstream;
                        break;

                    case Ast.DoOnFailure.Sync<T> dfs:
                        conts.Push(new DoOnFailureCont<T>(dfs.Action));
                        node = (INode<T>)dfs.Upstream;
                        break;

                    case Ast.DoOnFailure.Async<T> dfa:
                        conts.Push(new DoOnFailureAsyncCont<T>(dfa.AsyncAction));
                        node = (INode<T>)dfa.Upstream;
                        break;

                    case Ast.DoOnFailure.CancellableAsync<T> dfc:
                        conts.Push(new DoOnFailureCancellableCont<T>(dfc.AsyncAction));
                        node = (INode<T>)dfc.Upstream;
                        break;

                    // Validate (no type change)
                    case Ast.Validate.Sync<T> vs:
                        conts.Push(new ValidateCont<T>(vs.Predicate, vs.ExceptionFactory));
                        node = (INode<T>)vs.Upstream;
                        break;

                    case Ast.Validate.Async<T> va:
                        conts.Push(new ValidateAsyncCont<T>(va.PredicateAsync, va.ExceptionFactory));
                        node = (INode<T>)va.Upstream;
                        break;

                    case Ast.Validate.CancellableAsync<T> vc:
                        conts.Push(new ValidateCancellableCont<T>(vc.PredicateCancellableAsync, vc.ExceptionFactory));
                        node = (INode<T>)vc.Upstream;
                        break;

                    // Recover (no type change)
                    case Ast.Recover.Sync<T> rs:
                        conts.Push(new RecoverCont<T>(rs.Recover));
                        node = (INode<T>)rs.Source;
                        break;

                    case Ast.Recover.Async<T> ra:
                        conts.Push(new RecoverAsyncCont<T>(ra.Recover));
                        node = (INode<T>)ra.Source;
                        break;

                    case Ast.Recover.CancellableAsync<T> rc:
                        conts.Push(new RecoverCancellableCont<T>((ex, ct) => rc.Recover(ex, ct)));
                        node = (INode<T>)rc.Source;
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported node type: {node.GetType().FullName}");
                }
            }

            // Unwind: apply continuations
            var pushed = false;
            while (conts.Count > 0)
            {
                var cont = conts.Pop();
                var res = await cont.ApplyAsync(outcome!, options);
                switch (res)
                {
                    case OutcomeResult<T> r:
                        outcome = r.Outcome;
                        continue;
                    case PushFlow<T> p:
                        node = (INode<T>)p.Flow;
                        outcome = null;
                        pushed = true;
                        break;
                    default:
                        throw new NotSupportedException("Unknown frame result type.");
                }

                if (pushed) break;
            }

            if (!pushed)
            {
                return (Outcome<T>)outcome!;
            }
        }
    }

    private static bool IsAllNode<T>(INode<T> node) =>
        node.GetType().IsGenericType && node.GetType().GetGenericTypeDefinition() == typeof(Ast.Primitive.All<>);

    private static bool IsAnyNode<T>(INode<T> node) =>
        node.GetType().IsGenericType && node.GetType().GetGenericTypeDefinition() == typeof(Ast.Primitive.Any<>);

    private static async Task<Outcome<T>> EvaluateAllAsync<T>(INode<T> node, Options options)
    {
        var t = node.GetType();
        var elem = t.GetGenericArguments()[0];
        var method = typeof(Interpreter).GetMethod(nameof(EvaluateAllGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(elem);
        var task = (Task<object>)method.Invoke(null, new object[] { node, options })!;
        var resultObj = await task;
        return (Outcome<T>)resultObj;
    }

    private static async Task<Outcome<T>> EvaluateAnyAsync<T>(INode<T> node, Options options)
    {
        var t = node.GetType();
        var elem = t.GetGenericArguments()[0];
        var method = typeof(Interpreter).GetMethod(nameof(EvaluateAnyGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(elem);
        var task = (Task<object>)method.Invoke(null, new object[] { node, options })!;
        var resultObj = await task;
        return (Outcome<T>)resultObj;
    }

    private static async Task<object> EvaluateAllGeneric<TElement>(Ast.Primitive.All<TElement> all, Options options)
    {
        var tasks = all.Flows.Select(f => Interpreter.ExecuteAsync((INode<TElement>)f, options)).ToList();
        var outcomes = await Task.WhenAll(tasks);
        var exceptions = outcomes.OfType<Failure<TElement>>().Select(f => f.Exception).ToList();
        if (exceptions.Count > 0)
        {
            return Outcome.Failure<TElement[]>(new AggregateException(exceptions));
        }
        return Outcome.Success(outcomes.OfType<Success<TElement>>().Select(s => s.Value).ToArray());
    }

    private static async Task<object> EvaluateAnyGeneric<TElement>(Ast.Primitive.Any<TElement> any, Options options)
    {
        var tasks = any.Flows.Select(f => Interpreter.ExecuteAsync((INode<TElement>)f, options)).ToList();
        var outcome = await TryOperation.TryFindFirstSuccessfulFlow(tasks, []);
        return outcome;
    }
}
