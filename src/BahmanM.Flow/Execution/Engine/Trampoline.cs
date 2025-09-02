using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;
using BahmanM.Flow.Execution.Engine.Concurrency;
using BahmanM.Flow.Execution.Engine.Planning;
using BahmanM.Flow.Execution.Engine.Primitives;
using BahmanM.Flow.Execution.Engine.Resource;
using BahmanM.Flow.Execution.Engine.Unwind;
using BahmanM.Flow.Execution.Engine.Operators;

namespace BahmanM.Flow.Execution.Engine;

internal static class Interpreter
{
    internal static async Task<Outcome<T>> ExecuteAsync<T>(INode<T> root, Options options)
    {
        var continuations = new Stack<IContinuation<T>>();
        var currentNode = root;
        object? currentOutcome = null;

        while (true)
        {
            while (currentNode is not null)
            {
                // Composite (All/Any)
                var compositeOutcome = await ConcurrencyExecutor.TryHandleAsync(currentNode, options);
                if (compositeOutcome is not null)
                {
                    currentOutcome = compositeOutcome;
                    currentNode = null;
                    continue;
                }

                // Select/Chain planned frames
                var planResult = await FramePlanning.TryPlanAsync(currentNode, continuations, options);
                if (planResult.Handled)
                {
                    currentNode = planResult.NextNode;
                    if (planResult.Outcome is not null)
                    {
                        currentOutcome = planResult.Outcome;
                        currentNode = null;
                    }
                    continue;
                }

                // Resource
                var resourceResult = ResourceScope.TryOpen(currentNode, continuations);
                if (resourceResult.Handled)
                {
                    currentNode = resourceResult.NextNode;
                    if (resourceResult.Outcome is not null)
                    {
                        currentOutcome = resourceResult.Outcome;
                        currentNode = null;
                    }
                    continue;
                }

                // Leaves
                var primitiveOutcome = await PrimitiveExecutor.TryEvaluateAsync(currentNode, options);
                if (primitiveOutcome is not null)
                {
                    currentOutcome = primitiveOutcome;
                    currentNode = null;
                    continue;
                }

                // Push simple operator continuations
                if (!OperatorContinuationFactory.TryPush(ref currentNode, continuations))
                {
                    throw new NotSupportedException($"Unsupported node type: {currentNode.GetType().FullName}");
                }
            }

            var unwindState = await ContinuationUnwinder.UnwindAsync(continuations, currentOutcome!, options);
            if (unwindState.NextNode is not null)
            {
                currentNode = unwindState.NextNode;
                currentOutcome = null;
                continue;
            }
            return (Outcome<T>)unwindState.FinalOutcome!;
        }
    }
}
