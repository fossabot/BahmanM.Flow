using BahmanM.Flow.Ast;

namespace BahmanM.Flow.Execution.Engine.Primitives;

internal static class PrimitiveExecutor
{
    internal static async Task<object?> TryEvaluateAsync<T>(INode<T> node, Options options)
    {
        switch (node)
        {
            case Ast.Primitive.Succeed<T> s:
                return Outcome.Success(s.Value);
            case Ast.Primitive.Fail<T> f:
                return Outcome.Failure<T>(f.Exception);
            case Ast.Create.Sync<T> cSync:
                return await TryOperation.Sync<T>(() => cSync.Operation());
            case Ast.Create.Async<T> cAsync:
                return await TryOperation.Async<T>(() => cAsync.Operation());
            case Ast.Create.CancellableAsync<T> cCan:
                return await TryOperation.CancellableAsync<T>(ct => cCan.Operation(ct), options.CancellationToken);
        }
        return null;
    }
}

