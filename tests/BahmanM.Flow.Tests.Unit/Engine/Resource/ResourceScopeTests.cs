namespace BahmanM.Flow.Tests.Unit.Engine.Resource;

using BahmanM.Flow.Execution.Engine;
using BahmanM.Flow.Execution.Engine.Resource;
using BahmanM.Flow.Execution;

public class ResourceScopeTests
{
    private sealed class Dummy : IDisposable
    {
        public static int Disposed;
        public void Dispose() => Interlocked.Increment(ref Disposed);
    }

    [Fact]
    public void TryOpen_WhenAcquireThrows_ReturnsFailure_AndNoDisposer()
    {
        var flow = Flow.WithResource<Dummy, int>(() => throw new Exception("acquire"), _ => Flow.Succeed(1));
        var node = flow.AsNode();

        var continuations = new Stack<BahmanM.Flow.Execution.Continuations.IContinuation<int>>();
        var result = ResourceScope.TryOpen(node, continuations);

        Assert.True(result.Handled);
        Assert.Null(result.NextNode);
        var failure = Assert.IsType<Failure<int>>(result.Outcome);
        Assert.Equal("acquire", failure.Exception.Message);
        Assert.Empty(continuations);
    }

    [Fact]
    public void TryOpen_WhenUseThrows_ReturnsFailure_AndDisposerIsPushed()
    {
        Dummy.Disposed = 0;
        var flow = Flow.WithResource<Dummy, int>(() => new Dummy(), _ => throw new Exception("use"));
        var node = flow.AsNode();
        var continuations = new Stack<BahmanM.Flow.Execution.Continuations.IContinuation<int>>();

        var result = ResourceScope.TryOpen(node, continuations);
        Assert.True(result.Handled);
        Assert.Null(result.NextNode);
        Assert.IsType<Failure<int>>(result.Outcome);
        Assert.Single(continuations); // disposer pushed
    }

    [Fact]
    public void TryOpen_WhenUseSucceeds_ReturnsNextNode_AndDisposerIsPushed()
    {
        var flow = Flow.WithResource<Dummy, int>(() => new Dummy(), _ => Flow.Succeed(7));
        var node = flow.AsNode();
        var continuations = new Stack<BahmanM.Flow.Execution.Continuations.IContinuation<int>>();

        var result = ResourceScope.TryOpen(node, continuations);
        Assert.True(result.Handled);
        Assert.NotNull(result.NextNode);
        Assert.Null(result.Outcome);
        Assert.Single(continuations);
    }
}
