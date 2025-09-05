using BahmanM.Flow.Execution.Continuations;
using BahmanM.Flow.Ast;

namespace BahmanM.Flow.Execution.Engine;

internal sealed class InterpreterState<T>
{
    public INode<T>? CurrentNode { get; set; }
    public object? CurrentOutcome { get; set; }
    public Stack<IContinuation<T>> Continuations { get; }
    public Options Options { get; }

    public InterpreterState(INode<T> currentNode, Options options, Stack<IContinuation<T>>? continuations = null)
    {
        CurrentNode = currentNode;
        Options = options;
        Continuations = continuations ?? new Stack<IContinuation<T>>();
    }
}
