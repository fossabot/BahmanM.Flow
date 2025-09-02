namespace BahmanM.Flow.Execution.Planning.Select;

internal static class SelectFusion
{
    internal sealed class SelectPipeline<T>
    {
        private readonly List<Func<T, T>> _syncSteps = new();
        private readonly List<Func<T, CancellationToken, ValueTask<T>>> _steps = new();
        public bool UsesAsync { get; private set; }
        public bool UsesCancellation { get; private set; }

        public void AddSync(Func<T, T> f)
        {
            _syncSteps.Add(f);
            _steps.Add((x, _) => new ValueTask<T>(f(x)));
        }

        public void AddAsync(Func<T, Task<T>> f)
        {
            UsesAsync = true;
            _steps.Add(async (x, _) => await f(x));
        }

        public void AddCancellable(Func<T, CancellationToken, Task<T>> f)
        {
            UsesAsync = true;
            UsesCancellation = true;
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

    internal static (Ast.INode<T> UpstreamNode, SelectPipeline<T> Pipeline) Build<T>(
        IFlow<T> start,
        Flow.Operations.Select.Sync<T, T>? seedSync,
        Flow.Operations.Select.Async<T, T>? seedAsync,
        Flow.Operations.Select.CancellableAsync<T, T>? seedCanc)
    {
        var pipe = new SelectPipeline<T>();
        if (seedSync is not null) pipe.AddSync(x => seedSync(x));
        if (seedAsync is not null) pipe.AddAsync(x => seedAsync(x));
        if (seedCanc is not null) pipe.AddCancellable((x, ct) => seedCanc(x, ct));

        var upstream = start;
        while (true)
        {
            switch (upstream)
            {
                case Ast.Select.Sync<T, T> ss:
                    pipe.AddSync(x => ss.Operation(x));
                    upstream = ss.Upstream;
                    continue;
                case Ast.Select.Async<T, T> sa:
                    pipe.AddAsync(x => sa.Operation(x));
                    upstream = sa.Upstream;
                    continue;
                case Ast.Select.CancellableAsync<T, T> sc:
                    pipe.AddCancellable((x, ct) => sc.Operation(x, ct));
                    upstream = sc.Upstream;
                    continue;
            }
            break;
        }

        return (upstream.AsNode(), pipe);
    }
}
