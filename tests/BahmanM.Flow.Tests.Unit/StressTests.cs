using Xunit;

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
}
