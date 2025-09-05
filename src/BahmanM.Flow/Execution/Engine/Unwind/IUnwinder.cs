using BahmanM.Flow.Ast;

namespace BahmanM.Flow.Execution.Engine.Unwind;

internal interface IUnwinder<T>
{
    Task<Execution.Engine.UnwindState<T>> UnwindAsync(BahmanM.Flow.Execution.Engine.InterpreterState<T> state);
}

internal sealed class ContinuationUnwinderAdapter<T> : IUnwinder<T>
{
    public Task<Execution.Engine.UnwindState<T>> UnwindAsync(BahmanM.Flow.Execution.Engine.InterpreterState<T> state)
    {
        return ContinuationUnwinder.UnwindAsync(state.Continuations, state.CurrentOutcome!, state.Options);
    }
}

