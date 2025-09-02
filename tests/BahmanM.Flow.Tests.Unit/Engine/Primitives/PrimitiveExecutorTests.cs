namespace BahmanM.Flow.Tests.Unit.Engine.Primitives;

using BahmanM.Flow.Execution;
using BahmanM.Flow.Execution.Engine.Primitives;

public class PrimitiveExecutorTests
{
    [Fact]
    public async Task TryEvaluateAsync_CancellableCreate_RespectsCancelledToken()
    {
        var node = new BahmanM.Flow.Ast.Create.CancellableAsync<int>(ct => Task.FromResult(123));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var obj = await PrimitiveExecutor.TryEvaluateAsync(node, new Options(cts.Token));
        var failure = Assert.IsType<Failure<int>>(obj);
        Assert.IsType<TaskCanceledException>(failure.Exception);
    }
}

