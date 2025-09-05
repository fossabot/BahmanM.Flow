using BahmanM.Flow.Ast;

namespace BahmanM.Flow.Execution.Engine.Concurrency;

internal static class ConcurrencyHandler
{
    internal static async Task<DescendEffect<T>> TryHandleAsync<T>(INode<T> node, Options options)
    {
        var composite = await ConcurrencyExecutor.TryHandleAsync(node, options);
        if (composite is null) return new DescendEffect<T>(DescendEffectKind.NotHandled);
        return new DescendEffect<T>(DescendEffectKind.SetOutcome, Outcome: composite);
    }
}
