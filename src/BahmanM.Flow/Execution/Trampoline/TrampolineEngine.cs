using System.Reflection;
using BahmanM.Flow.Ast;

namespace BahmanM.Flow.Execution.Trampoline;

internal static class TrampolineEngine
{
    internal static async Task<Outcome<T>> RunAsync<T>(INode<T> root, Options options)
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

                // Handle Select/Chain via typed continuations and trampoline evaluation of upstream
                if (IsSelectNode(node))
                {
                    var (cont, upstream, tIn) = PrepareSelectCont(node);
                    conts.Push(cont);
                    outcome = await RunAsyncObject(upstream, tIn, options);
                    node = null;
                    continue;
                }
                if (IsChainNode(node))
                {
                    var (cont, upstream, tIn) = PrepareChainCont(node);
                    conts.Push(cont);
                    outcome = await RunAsyncObject(upstream, tIn, options);
                    node = null;
                    continue;
                }

                // Handle WithResource<TResource,T> via a generic helper (avoid reflection in hot path later)
                if (IsWithResourceNode(node))
                {
                    var prep = PrepareWithResource(node);
                    if (prep.EarlyOutcome is not null)
                    {
                        outcome = prep.EarlyOutcome;
                        node = null;
                    }
                    else
                    {
                        conts.Push(prep.DisposeCont!);
                        node = prep.Inner!;
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

    private static bool IsWithResourceNode<T>(INode<T> node)
    {
        var t = node.GetType();
        return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Ast.Resource.WithResource<,>);
    }

    private static bool IsAllNode<T>(INode<T> node) =>
        node.GetType().IsGenericType && node.GetType().GetGenericTypeDefinition() == typeof(Ast.Primitive.All<>);

    private static bool IsAnyNode<T>(INode<T> node) =>
        node.GetType().IsGenericType && node.GetType().GetGenericTypeDefinition() == typeof(Ast.Primitive.Any<>);

    private static async Task<Outcome<T>> EvaluateAllAsync<T>(INode<T> node, Options options)
    {
        var t = node.GetType();
        var elem = t.GetGenericArguments()[0];
        var method = typeof(TrampolineEngine).GetMethod(nameof(EvaluateAllGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(elem);
        var task = (Task<object>)method.Invoke(null, new object[] { node, options })!;
        var resultObj = await task;
        return (Outcome<T>)resultObj;
    }

    private static async Task<Outcome<T>> EvaluateAnyAsync<T>(INode<T> node, Options options)
    {
        var t = node.GetType();
        var elem = t.GetGenericArguments()[0];
        var method = typeof(TrampolineEngine).GetMethod(nameof(EvaluateAnyGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(elem);
        var task = (Task<object>)method.Invoke(null, new object[] { node, options })!;
        var resultObj = await task;
        return (Outcome<T>)resultObj;
    }

    private static async Task<object> EvaluateAllGeneric<TElement>(Ast.Primitive.All<TElement> all, Options options)
    {
        var tasks = all.Flows.Select(f => RunAsync((INode<TElement>)f, options)).ToList();
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
        var tasks = any.Flows.Select(f => RunAsync((INode<TElement>)f, options)).ToList();
        var outcome = await TryOperation.TryFindFirstSuccessfulFlow(tasks, []);
        return outcome;
    }

    private static bool IsSelectNode<T>(INode<T> node)
    {
        var t = node.GetType();
        if (!t.IsGenericType) return false;
        var def = t.GetGenericTypeDefinition();
        return def == typeof(Ast.Select.Sync<,>) || def == typeof(Ast.Select.Async<,>) || def == typeof(Ast.Select.CancellableAsync<,>);
    }

    private static bool IsChainNode<T>(INode<T> node)
    {
        var t = node.GetType();
        if (!t.IsGenericType) return false;
        var def = t.GetGenericTypeDefinition();
        return def == typeof(Ast.Chain.Sync<,>) || def == typeof(Ast.Chain.Async<,>) || def == typeof(Ast.Chain.CancellableAsync<,>);
    }

    private static (IContinuation<T> cont, object upstream, Type tIn) PrepareSelectCont<T>(INode<T> node)
    {
        var t = node.GetType();
        var gargs = t.GetGenericArguments();
        var tIn = gargs[0];
        var def = t.GetGenericTypeDefinition();

        if (def == typeof(Ast.Select.Sync<,>))
        {
            dynamic dyn = node;
            var op = (Delegate)dyn.Operation;
            var contType = typeof(SelectCont<,>).MakeGenericType(tIn, typeof(T));
            var cont = (IContinuation<T>)Activator.CreateInstance(contType, op)!;
            var upstream = (object)dyn.Upstream;
            return (cont, upstream, tIn);
        }
        if (def == typeof(Ast.Select.Async<,>))
        {
            dynamic dyn = node;
            var op = (Delegate)dyn.Operation;
            var contType = typeof(SelectAsyncCont<,>).MakeGenericType(tIn, typeof(T));
            var cont = (IContinuation<T>)Activator.CreateInstance(contType, op)!;
            var upstream = (object)dyn.Upstream;
            return (cont, upstream, tIn);
        }
        // Cancellable
        {
            dynamic dyn = node;
            var op = (Delegate)dyn.Operation;
            var contType = typeof(SelectCancellableCont<,>).MakeGenericType(tIn, typeof(T));
            var cont = (IContinuation<T>)Activator.CreateInstance(contType, op)!;
            var upstream = (object)dyn.Upstream;
            return (cont, upstream, tIn);
        }
    }

    private static (IContinuation<T> cont, object upstream, Type tIn) PrepareChainCont<T>(INode<T> node)
    {
        var t = node.GetType();
        var gargs = t.GetGenericArguments();
        var tIn = gargs[0];
        var def = t.GetGenericTypeDefinition();

        if (def == typeof(Ast.Chain.Sync<,>))
        {
            dynamic dyn = node;
            var op = (Delegate)dyn.Operation;
            var contType = typeof(ChainCont<,>).MakeGenericType(tIn, typeof(T));
            var cont = (IContinuation<T>)Activator.CreateInstance(contType, op)!;
            var upstream = (object)dyn.Upstream;
            return (cont, upstream, tIn);
        }
        if (def == typeof(Ast.Chain.Async<,>))
        {
            dynamic dyn = node;
            var op = (Delegate)dyn.Operation;
            var contType = typeof(ChainAsyncCont<,>).MakeGenericType(tIn, typeof(T));
            var cont = (IContinuation<T>)Activator.CreateInstance(contType, op)!;
            var upstream = (object)dyn.Upstream;
            return (cont, upstream, tIn);
        }
        // Cancellable
        {
            dynamic dyn = node;
            var op = (Delegate)dyn.Operation;
            var contType = typeof(ChainCancellableCont<,>).MakeGenericType(tIn, typeof(T));
            var cont = (IContinuation<T>)Activator.CreateInstance(contType, op)!;
            var upstream = (object)dyn.Upstream;
            return (cont, upstream, tIn);
        }
    }

    private static Task<object> RunAsyncObject(object upstreamNode, Type tIn, Options options)
    {
        var method = typeof(TrampolineEngine).GetMethod(nameof(RunAsyncObjectGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(tIn);
        return (Task<object>)method.Invoke(null, new object[] { upstreamNode, options })!;
    }

    private static async Task<object> RunAsyncObjectGeneric<TIn>(object upstreamNode, Options options)
    {
        return await RunAsync((INode<TIn>)upstreamNode, options);
    }

    private sealed record WithResourcePrep<T>(IContinuation<T>? DisposeCont, INode<T>? Inner, Outcome<T>? EarlyOutcome);

    private static WithResourcePrep<T> PrepareWithResource<T>(INode<T> node)
    {
        var t = node.GetType();
        var gargs = t.GetGenericArguments();
        var tResource = gargs[0];
        var method = typeof(TrampolineEngine)
            .GetMethod(nameof(PrepareWithResourceGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var gmethod = method.MakeGenericMethod(tResource, typeof(T));
        return (WithResourcePrep<T>)gmethod.Invoke(null, new object[] { node })!;
    }

    private static WithResourcePrep<T> PrepareWithResourceGeneric<TResource, T>(Ast.Resource.WithResource<TResource, T> wr)
        where TResource : IDisposable
    {
        TResource resource;
        try
        {
            resource = wr.Acquire();
        }
        catch (Exception ex)
        {
            return new WithResourcePrep<T>(DisposeCont: null, Inner: null, EarlyOutcome: Outcome.Failure<T>(ex));
        }

        var disposeCont = new DisposeCont<TResource, T>(resource);

        try
        {
            var inner = (INode<T>)wr.Use(resource);
            return new WithResourcePrep<T>(disposeCont, inner, null);
        }
        catch (Exception useEx)
        {
            try
            {
                resource.Dispose();
            }
            catch (Exception disposeEx)
            {
                return new WithResourcePrep<T>(null, null, Outcome.Failure<T>(disposeEx));
            }
            return new WithResourcePrep<T>(null, null, Outcome.Failure<T>(useEx));
        }
    }
}
