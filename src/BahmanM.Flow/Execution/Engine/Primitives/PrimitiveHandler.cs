using BahmanM.Flow.Ast;

namespace BahmanM.Flow.Execution.Engine.Primitives;

internal static class PrimitiveHandler
{
    internal static async Task<DescendEffect<T>> TryHandleAsync<T>(INode<T> node, Options options)
    {
        var primitive = await PrimitiveExecutor.TryEvaluateAsync(node, options);
        if (primitive is null) return new DescendEffect<T>(DescendEffectKind.NotHandled);
        return new DescendEffect<T>(DescendEffectKind.SetOutcome, Outcome: primitive);
    }
}
