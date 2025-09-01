namespace BahmanM.Flow.Execution.Engine;

internal abstract record FrameResult<T>;

internal sealed record OutcomeResult<T>(Outcome<T> Outcome) : FrameResult<T>;

internal sealed record PushFlow<T>(IFlow<T> Flow) : FrameResult<T>;
