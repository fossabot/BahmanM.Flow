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
        var conts = new Stack<IContinuation<T>>();
        var node = root;
        object? outcome = null;

        while (true)
        {
            while (node is not null)
            {
                // Composite (All/Any)
                var composite = await ConcurrencyExecutor.TryHandleAsync(node, options);
                if (composite is not null)
                {
                    outcome = composite;
                    node = null;
                    continue;
                }

                // Select/Chain planned frames
                var planned = await FramePlanning.TryPlanAsync(node, conts, options);
                if (planned.Handled)
                {
                    node = planned.NextNode;
                    if (planned.Outcome is not null)
                    {
                        outcome = planned.Outcome;
                        node = null;
                    }
                    continue;
                }

                // Resource
                var resource = ResourceScope.TryOpen(node, conts);
                if (resource.Handled)
                {
                    node = resource.NextNode;
                    if (resource.Outcome is not null)
                    {
                        outcome = resource.Outcome;
                        node = null;
                    }
                    continue;
                }

                // Leaves
                var leaf = await PrimitiveExecutor.TryEvaluateAsync(node, options);
                if (leaf is not null)
                {
                    outcome = leaf;
                    node = null;
                    continue;
                }

                // Push simple operator continuations
                if (!OperatorContinuationFactory.TryPush(ref node, conts))
                {
                    throw new NotSupportedException($"Unsupported node type: {node.GetType().FullName}");
                }
            }

            var unwind = await ContinuationUnwinder.UnwindAsync(conts, outcome!, options);
            if (unwind.NextNode is not null)
            {
                node = unwind.NextNode;
                outcome = null;
                continue;
            }
            return (Outcome<T>)unwind.FinalOutcome!;
        }
    }
}
