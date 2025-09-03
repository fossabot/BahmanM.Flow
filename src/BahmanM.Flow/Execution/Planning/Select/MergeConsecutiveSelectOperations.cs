namespace BahmanM.Flow.Execution.Planning.Select;

internal static class MergeConsecutiveSelectOperations
{
    internal sealed class SelectOperationSequence<T>
    {
        private readonly List<Func<T, T>> _syncSteps = new();
        private readonly List<Func<T, CancellationToken, ValueTask<T>>> _steps = new();
        public bool RequiresAsynchronousExecution { get; private set; }
        public bool RequiresCancellationToken { get; private set; }

        public void AddSync(Func<T, T> f)
        {
            _syncSteps.Add(f);
            _steps.Add((x, _) => new ValueTask<T>(f(x)));
        }

        public void AddAsync(Func<T, Task<T>> f)
        {
            RequiresAsynchronousExecution = true;
            _steps.Add(async (x, _) => await f(x));
        }

        public void AddCancellable(Func<T, CancellationToken, Task<T>> f)
        {
            RequiresAsynchronousExecution = true;
            RequiresCancellationToken = true;
            _steps.Add(async (x, ct) => await f(x, ct));
        }

        public T RunSync(T input)
        {
            return _syncSteps
                .Aggregate(
                    input,
                    (current, step) =>
                        step(current));
        }

        public async ValueTask<T> Run(T input, CancellationToken ct)
        {
            var acc = input;
            foreach (var step in _steps)
            {
                acc = await step(acc, ct);
            }
            return acc;
        }
    }

    internal static (Ast.INode<T> UpstreamNode, SelectOperationSequence<T> Sequence) MergeAdjacent<T>(
        IFlow<T> start,
        Flow.Operations.Select.Sync<T, T>? seedSync,
        Flow.Operations.Select.Async<T, T>? seedAsync,
        Flow.Operations.Select.CancellableAsync<T, T>? seedCanc)
    {
        var sequence = new SelectOperationSequence<T>();
        if (seedSync is not null) sequence.AddSync(x => seedSync(x));
        if (seedAsync is not null) sequence.AddAsync(x => seedAsync(x));
        if (seedCanc is not null) sequence.AddCancellable((x, ct) => seedCanc(x, ct));

        var upstreamFlow = start;
        while (true)
        {
            switch (upstreamFlow)
            {
                case Ast.Select.Sync<T, T> selectSync:
                    sequence.AddSync(x => selectSync.Operation(x));
                    upstreamFlow = selectSync.Upstream;
                    continue;
                case Ast.Select.Async<T, T> selectAsync:
                    sequence.AddAsync(x => selectAsync.Operation(x));
                    upstreamFlow = selectAsync.Upstream;
                    continue;
                case Ast.Select.CancellableAsync<T, T> selectCancellable:
                    sequence.AddCancellable((x, ct) => selectCancellable.Operation(x, ct));
                    upstreamFlow = selectCancellable.Upstream;
                    continue;
            }
            break;
        }

        return (upstreamFlow.AsNode(), sequence);
    }
}
