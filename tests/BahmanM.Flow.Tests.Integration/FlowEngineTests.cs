using Xunit;

namespace BahmanM.Flow.Tests.Integration;

[Collection("NonFunctionalSerial")]
public class FlowEngineTests
{
    [Fact]
    [Trait("Category", "NonFunctional")]
    public async Task ExecuteAsync_WithLongChain_DoesNotCauseStackOverflow()
    {
        // Arrange
        const int chainLength = 20_000;
        var flow = Flow.Succeed(0);
        var counter = 0;

        for (var i = 0; i < chainLength; i++)
        {
            flow = flow.DoOnSuccess(_ => counter++);
        }

        // Act
        var outcome = await FlowEngine.ExecuteAsync(flow);

        // Assert
        Assert.IsType<Success<int>>(outcome);
        Assert.Equal(chainLength, counter);
    }
}
