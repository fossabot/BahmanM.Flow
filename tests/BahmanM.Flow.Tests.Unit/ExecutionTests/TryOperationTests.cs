using BahmanM.Flow.Execution;

namespace BahmanM.Flow.Tests.Unit.ExecutionTests;

public class TryOperationTests
{
    [Fact]
    public async Task TryFindFirstSuccessfulFlow_Returns_First_Success()
    {
        var tcs1 = Task.Run(async () => { await Task.Delay(20); return Outcome.Failure<int>(new Exception("f1")); });
        var tcs2 = Task.Run(async () => { await Task.Delay(5); return Outcome.Success(42); });
        var tcs3 = Task.Run(async () => { await Task.Delay(30); return Outcome.Success(99); });

        var remaining = new List<Task<Outcome<int>>> { tcs1, tcs2, tcs3 };
        var result = await TryOperation.TryFindFirstSuccessfulFlow(remaining, []);

        var success = Assert.IsType<Success<int>>(result);
        Assert.Equal(42, success.Value);
    }

    [Fact]
    public async Task TryFindFirstSuccessfulFlow_Aggregates_Exceptions_When_All_Fail()
    {
        var t1 = Task.FromResult<Outcome<int>>(Outcome.Failure<int>(new InvalidOperationException()));
        var t2 = Task.FromResult<Outcome<int>>(Outcome.Failure<int>(new ApplicationException()));

        var result = await TryOperation.TryFindFirstSuccessfulFlow([t1, t2], []);

        var failure = Assert.IsType<Failure<int>>(result);
        Assert.IsType<AggregateException>(failure.Exception);
        var agg = (AggregateException)failure.Exception;
        Assert.Equal(2, agg.InnerExceptions.Count);
    }
}
