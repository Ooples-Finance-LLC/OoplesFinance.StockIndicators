using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RemainingSpreadCandleBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "RelativeSpreadStrength", "RetrospectiveCandlestickChart" }
        from count in new[] { 1_000, 10_000 }
        from history in Enum.GetValues<IndicatorHistoryMode>()
        where operation != "RelativeSpreadStrength" || count != 10_000 ||
            history is not (IndicatorHistoryMode.Full or IndicatorHistoryMode.LatestOnly)
        select new object[] { operation, count, history };

    [Theory, MemberData(nameof(Cases))]
    public void BothMeasuredArmsPreserveEveryOutputBit(string operation, int count, IndicatorHistoryMode history)
        => Check(operation, count, history);

    [Fact]
    public void RelativeSpreadFull10000() => Check("RelativeSpreadStrength", 10_000, IndicatorHistoryMode.Full);

    [Fact]
    public void RelativeSpreadLatest10000() => Check("RelativeSpreadStrength", 10_000, IndicatorHistoryMode.LatestOnly);

    private static void Check(string operation, int count, IndicatorHistoryMode history)
    {
        var fixture = new RemainingSpreadCandleBenchmarks { Operation = operation, Count = count, History = history };
        fixture.Setup();
        Assert.Equal(BitConverter.DoubleToInt64Bits(fixture.Legacy()), BitConverter.DoubleToInt64Bits(fixture.Shared()));
    }
}
