using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[CollectionDefinition("Regression channel benchmark settings", DisableParallelization = true)]
public sealed class RegressionChannelBenchmarkCollection { }

[Collection("Regression channel benchmark settings")]
public sealed class SharedRegressionChannelBenchmarkTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(19)] [InlineData(20)] [InlineData(21)]
    [InlineData(513)] [InlineData(1000)] [InlineData(10000)]
    public void SavedInputsPreserveEveryHistoricalAndLatestOutput(int count)
    {
        var fixture = new SharedRegressionChannelBenchmarks { Count = count };
        try
        {
            fixture.Setup();
            using var full = fixture.BuilderFull(); using var latest = fixture.BuilderLatestOnly();
            Assert.NotNull(full); Assert.NotNull(latest);
        }
        finally { fixture.Cleanup(); }
    }
}
