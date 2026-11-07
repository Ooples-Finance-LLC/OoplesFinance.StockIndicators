using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RemainingVervoortModifiedBenchmarkTests
{
    [Theory]
    [InlineData(IndicatorHistoryMode.Full)]
    [InlineData(IndicatorHistoryMode.LatestOnly)]
    public void BothMeasuredArmsPreserveEveryOutputBit(IndicatorHistoryMode history) => Verify(1_000, history);

    // Independently schedule the longer exact recurrences without reducing coverage.
    [Fact] public void ModifiedFull10000() => Verify(10_000, IndicatorHistoryMode.Full);
    [Fact] public void ModifiedLatest10000() => Verify(10_000, IndicatorHistoryMode.LatestOnly);

    private static void Verify(int count, IndicatorHistoryMode history)
    {
        var fixture = new RemainingVervoortModifiedBenchmarks { Count = count, History = history };
        fixture.Setup();
        Assert.Equal(BitConverter.DoubleToInt64Bits(fixture.Legacy()), BitConverter.DoubleToInt64Bits(fixture.Shared()));
    }
}
