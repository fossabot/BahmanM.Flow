using BahmanM.Flow.Ast;

namespace BahmanM.Flow.Execution.Trampoline;

internal static class TrampolineEngine
{
    internal static async Task<Outcome<T>> RunAsync<T>(INode<T> root, Options options)
    {
        var conts = new Stack<IContinuation<T>>();
        INode<T>? node = root;
        Outcome<T>? outcome = null;

        while (true)
        {
            // Descend: push continuations; evaluate leaves
            while (node is not null)
            {
                // Temporary fallback for type-changing or composite nodes (Select/Chain/All/Any): evaluate remainder via recursive interpreter
                if (IsTypeChangingNode(node) || IsCompositeNode(node))
                {
                    outcome = await node.Accept(new Execution.Interpreter(options));
                    node = null;
                    continue;
                }

                // Handle WithResource<TResource,T> via a generic helper (avoid reflection in hot path later)
                if (IsWithResourceNode(node))
                {
                    (outcome, node) = await HandleWithResourceAsync(node, options);
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
                return outcome!;
            }
        }
    }

    private static bool IsTypeChangingNode<T>(INode<T> node)
    {
        var t = node.GetType();
        if (!t.IsGenericType) return false;
        var def = t.GetGenericTypeDefinition();
        return def == typeof(Ast.Select.Sync<,>)
               || def == typeof(Ast.Select.Async<,>)
               || def == typeof(Ast.Select.CancellableAsync<,>)
               || def == typeof(Ast.Chain.Sync<,>)
               || def == typeof(Ast.Chain.Async<,>)
               || def == typeof(Ast.Chain.CancellableAsync<,>);
    }

    private static bool IsWithResourceNode<T>(INode<T> node)
    {
        var t = node.GetType();
        return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Ast.Resource.WithResource<,>);
    }

    private static bool IsCompositeNode<T>(INode<T> node)
    {
        var t = node.GetType();
        if (!t.IsGenericType) return false;
        var def = t.GetGenericTypeDefinition();
        return def == typeof(Ast.Primitive.All<>) || def == typeof(Ast.Primitive.Any<>);
    }

    private static async Task<(Outcome<T> outcome, INode<T>? next)> HandleWithResourceAsync<T>(INode<T> node, Options options)
    {
        // Use dynamic to invoke the generic helper with inferred TResource (limited usage kept in one place)
        return await HandleWithResourceDynamic((dynamic)node, options);
    }

    private static async Task<(Outcome<T> outcome, INode<T>? next)> HandleWithResourceDynamic<TResource, T>(Ast.Resource.WithResource<TResource, T> wr, Options options)
        where TResource : IDisposable
    {
        TResource resource;
        try { resource = wr.Acquire(); }
        catch (Exception ex)
        {
            return (Outcome.Failure<T>(ex), null);
        }

        // Ensure disposal on unwind
        var disposeCont = new DisposeCont<TResource, T>(resource);

        try
        {
            var inner = (INode<T>)wr.Use(resource);
            // We cannot push from here; the caller will push cont via returned next node
            // Instead, we return the next node and let the caller push the dispose continuation onto the stack.
            // However, pushing from caller requires passing back the continuation; to keep interface simple,
            // we attach it via a small trick: we return a Success<T> outcome indicating no-op and set next to inner,
            // then the caller has to push disposeCont. Since we cannot pass the cont here, we change approach:
        }
        catch (Exception ex)
        {
            // If Use throws before producing inner flow, return failure and no next node.
            return (Outcome.Failure<T>(ex), null);
        }

        // The caller needs the dispose continuation; since our main loop manages the stack, we'll push here by side-effect.
        // To enable that, we cannot from here; adjust approach: return a marker outcome and next node; caller will detect marker.
        // For simplicity in the current skeleton, just execute the inner flow via trampoline and then dispose before returning.
        try
        {
            var innerNode = (INode<T>)wr.Use(resource);
            var outcome = await RunAsync(innerNode, options);
            // Dispose (dominates on exception)
            try { resource.Dispose(); }
            catch (Exception dex) { return (Outcome.Failure<T>(dex), null); }
            return (outcome, null);
        }
        catch (Exception ex)
        {
            try { resource.Dispose(); } catch (Exception dex) { return (Outcome.Failure<T>(dex), null); }
            return (Outcome.Failure<T>(ex), null);
        }
    }
}
