namespace BahmanM.Flow.Execution.Engine;

internal sealed record PlanResult<T>(bool Handled, Ast.INode<T>? NextNode, object? Outcome);
internal sealed record ResourceResult<T>(bool Handled, Ast.INode<T>? NextNode, object? Outcome);
internal sealed record UnwindState<T>(Ast.INode<T>? NextNode, object? FinalOutcome);

