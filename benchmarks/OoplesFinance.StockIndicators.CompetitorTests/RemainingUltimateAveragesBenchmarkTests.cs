using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RemainingUltimateAveragesBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "UltimateMovingAverage", "UltimateMovingAverageBands" }
        from count in new[] { 1_000, 10_000 }
        from history in Enum.GetValues<IndicatorHistoryMode>()
        where count != 10_000 || history is not (IndicatorHistoryMode.Full or IndicatorHistoryMode.LatestOnly)
        select new object[] { operation, count, history };

    [Theory, MemberData(nameof(Cases))]
    public void BothMeasuredArmsPreserveEveryOutputBit(string operation, int count, IndicatorHistoryMode history)
        => Check(operation, count, history);

    [Fact]
    public void UltimateFull10000() => Check("UltimateMovingAverage", 10_000, IndicatorHistoryMode.Full);

    [Fact]
    public void UltimateLatest10000() => Check("UltimateMovingAverage", 10_000, IndicatorHistoryMode.LatestOnly);

    [Fact]
    public void UltimateBandsFull10000() => Check("UltimateMovingAverageBands", 10_000, IndicatorHistoryMode.Full);

    [Fact]
    public void UltimateBandsLatest10000() => Check("UltimateMovingAverageBands", 10_000, IndicatorHistoryMode.LatestOnly);

    private static void Check(string operation, int count, IndicatorHistoryMode history)
    {
        var fixture = new RemainingUltimateAveragesBenchmarks { Operation = operation, Count = count, History = history };
        fixture.Setup();
        Assert.Equal(BitConverter.DoubleToInt64Bits(fixture.Legacy()), BitConverter.DoubleToInt64Bits(fixture.Shared()));
    }
}
