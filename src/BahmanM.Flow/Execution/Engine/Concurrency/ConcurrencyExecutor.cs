using System.Reflection;
using BahmanM.Flow.Ast;

namespace BahmanM.Flow.Execution.Engine.Concurrency;

internal static class ConcurrencyExecutor
{
    internal static async Task<object?> TryHandleAsync<T>(INode<T> node, Options options)
    {
        if (IsAllNode(node)) return await EvaluateAllAsync(node, options);
        if (IsAnyNode(node)) return await EvaluateAnyAsync(node, options);
        return null;
    }

    private static bool IsAllNode<T>(INode<T> node) =>
        node.GetType().IsGenericType && node.GetType().GetGenericTypeDefinition() == typeof(Ast.Primitive.All<>);

    private static bool IsAnyNode<T>(INode<T> node) =>
        node.GetType().IsGenericType && node.GetType().GetGenericTypeDefinition() == typeof(Ast.Primitive.Any<>);

    private static async Task<Outcome<T>> EvaluateAllAsync<T>(INode<T> node, Options options)
    {
        var t = node.GetType();
        var elem = t.GetGenericArguments()[0];
        var method = typeof(ConcurrencyExecutor).GetMethod(nameof(EvaluateAllGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(elem);
        var task = (Task<object>)method.Invoke(null, [node, options])!;
        var resultObj = await task;
        return (Outcome<T>)resultObj;
    }

    private static async Task<Outcome<T>> EvaluateAnyAsync<T>(INode<T> node, Options options)
    {
        var t = node.GetType();
        var elem = t.GetGenericArguments()[0];
        var method = typeof(ConcurrencyExecutor).GetMethod(nameof(EvaluateAnyGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(elem);
        var task = (Task<object>)method.Invoke(null, [node, options])!;
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
