using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RemainingWilsonBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "WilsonRelativePriceChannel" }
        from count in new[] { 1_000 }
        from history in Enum.GetValues<IndicatorHistoryMode>()
        select new object[] { operation, count, history };

    [Theory, MemberData(nameof(Cases))]
    public void BothMeasuredArmsPreserveEveryOutputBit(string operation, int count, IndicatorHistoryMode history)
        => Verify(operation, count, history);

    // Keep each long exact recurrence independently schedulable on hosted runners.
    [Fact]
    public void WilsonFull10000() => Verify("WilsonRelativePriceChannel", 10_000, IndicatorHistoryMode.Full);

    [Fact]
    public void WilsonLatest10000() => Verify("WilsonRelativePriceChannel", 10_000, IndicatorHistoryMode.LatestOnly);

    private static void Verify(string operation, int count, IndicatorHistoryMode history)
    {
        var fixture = new RemainingWilsonBenchmarks { Operation = operation, Count = count, History = history };
        fixture.Setup();
        Assert.Equal(BitConverter.DoubleToInt64Bits(fixture.Legacy()),
            BitConverter.DoubleToInt64Bits(fixture.Shared()));
    }
}
