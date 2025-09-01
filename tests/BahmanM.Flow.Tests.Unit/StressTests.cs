namespace BahmanM.Flow.Tests.Unit;

[Collection("NonFunctionalSerial")]
public class StressTests
{
    [Fact]
    [Trait("Category", "NonFunctional")]
    public async Task Select_50k_Transforms_Correctly()
    {
        const int depth = 50_000;
        var flow = Flow.Succeed(0);
        for (var i = 0; i < depth; i++)
        {
            flow = flow.Select(x => x + 1);
        }

        var outcome = await FlowEngine.ExecuteAsync(flow);
        var success = Assert.IsType<Success<int>>(outcome);
        Assert.Equal(depth, success.Value);
    }

    [Fact]
    [Trait("Category", "NonFunctional")]
    public async Task Chain_50k_Sequences_Correctly()
    {
        const int depth = 50_000;
        var flow = Flow.Succeed(0);
        for (var i = 0; i < depth; i++)
        {
            flow = flow.Chain(x => Flow.Succeed(x + 1));
        }

        var outcome = await FlowEngine.ExecuteAsync(flow);
        var success = Assert.IsType<Success<int>>(outcome);
        Assert.Equal(depth, success.Value);
    }

    [Fact]
    [Trait("Category", "NonFunctional")]
    public async Task Combo_Deep_Select_Chain_Validate_200001335472347()
    {
        const int depth = 20_000;
        var flow = Flow.Succeed(0);
        for (var i = 1; i <= depth; i++)
        {
            if ((i & 1) == 0)
                flow = flow.Select(x => x + 1);
            else
                flow = flow.Chain(x => Flow.Succeed(x + 1));

            if (i % 500 == 0)
                flow = flow.Validate(x => x >= 0, x => new Exception("invalid"));
        }

        var outcome = await FlowEngine.ExecuteAsync(flow);
        var success = Assert.IsType<Success<int>>(outcome);
        Assert.Equal(depth, success.Value);
    }

    [Fact]
    [Trait("Category", "NonFunctional")]
    public async Task Combo_Recover_On_Validation_Failure_10000915239779()
    {
        const int depth = 10_000;
        var flow = Flow.Succeed(0);
        for (var i = 1; i <= depth; i++)
        {
            flow = flow.Select(x => x + 1);
            if (i % 1000 == 0)
            {
                // Force a failure and recover to a known value to continue
                var snapshot = i; // capture iteration at composition time
                flow = flow
                    .Validate(_ => false, _ => new Exception("forced"))
                    .Recover(_ => Flow.Succeed(snapshot));
            }
        }

        var outcome = await FlowEngine.ExecuteAsync(flow);
        var success = Assert.IsType<Success<int>>(outcome);
        Assert.Equal(depth, success.Value);
    }

    public sealed class Dummy : IDisposable
    {
        public static int Disposed;
        public void Dispose() => Interlocked.Increment(ref Disposed);
    }

    [Fact]
    [Trait("Category", "NonFunctional")]
    public async Task Combo_WithResource_5000_Disposes_All979057064()
    {
        Dummy.Disposed = 0;
        const int depth = 5_000;
        var flow = Flow.Succeed(0);
        for (var i = 1; i <= depth; i++)
        {
            flow = flow.Chain(v => Flow.WithResource(() => new Dummy(), _ => Flow.Succeed(v + 1)));
        }

        var outcome = await FlowEngine.ExecuteAsync(flow);
        var success = Assert.IsType<Success<int>>(outcome);
        Assert.Equal(depth, success.Value);
        Assert.Equal(depth, Dummy.Disposed);
    }

    [Fact]
    [Trait("Category", "NonFunctional")]
    public async Task Combo_All_Any_Every_50_Steps_5000309027265()
    {
        const int depth = 5_000;
        var flow = Flow.Succeed(0);
        for (var i = 1; i <= depth; i++)
        {
            if (i % 50 == 0)
            {
                flow = flow.Chain(v =>
                    Flow.All(Flow.Succeed(v + 1), Flow.Succeed(v + 2), Flow.Succeed(v + 3))
                        .Select(vals => vals[0])
                );
            }
            else if (i % 75 == 0)
            {
                flow = flow.Chain(v => Flow.Any(Flow.Fail<int>(new Exception()), Flow.Succeed(v + 1)));
            }
            else
            {
                flow = flow.Select(x => x + 1);
            }
        }

        var outcome = await FlowEngine.ExecuteAsync(flow);
        var success = Assert.IsType<Success<int>>(outcome);
        Assert.Equal(depth, success.Value);
    }

    [Fact]
    [Trait("Category", "NonFunctional")]
    public async Task Combo_DoOnSuccess_DoOnFailure_Counters_1000072718334()
    {
        const int depth = 10_000;
        var successCount = 0;
        var failureCount = 0;
        var flow = Flow.Succeed(0);

        for (var i = 1; i <= depth; i++)
        {
            flow = flow.DoOnSuccess(_ => successCount++);
            if (i % 2000 == 0)
            {
                flow = flow
                    .Chain<int, int>(_ => Flow.Fail<int>(new Exception("boom")))
                    .DoOnFailure(_ => failureCount++)
                    .Recover(_ => Flow.Succeed(0));
            }
            else
            {
                flow = flow.Select(x => x + 1);
            }
        }

        var outcome = await FlowEngine.ExecuteAsync(flow);
        var success = Assert.IsType<Success<int>>(outcome);
        // Final step is a fail/recover boundary; value resets to 0 at the end of each 2000-step window
        Assert.Equal(depth % 2000, success.Value);
        Assert.Equal(depth / 2000, failureCount);
        Assert.True(successCount > 0);
    }
}
