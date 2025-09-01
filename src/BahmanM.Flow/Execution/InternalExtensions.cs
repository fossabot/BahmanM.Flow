namespace BahmanM.Flow.Execution;

internal static class InternalExtensions
{
    internal static BahmanM.Flow.Ast.INode<T> AsNode<T>(this IFlow<T> flow) => (BahmanM.Flow.Ast.INode<T>)flow;
}

