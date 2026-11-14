using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RemainingRangeHealthBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Demarker", "DeMarker", "UlcerIndex", "ChoppinessIndex" }
        from count in new[] { 1_000, 10_000 }
        from history in Enum.GetValues<IndicatorHistoryMode>()
        select new object[] { operation, count, history };

    [Theory, MemberData(nameof(Cases))]
    public void BothMeasuredArmsPreserveEveryOutputBit(string operation, int count, IndicatorHistoryMode history)
    {
        var fixture = new RemainingRangeHealthBenchmarks { Operation = operation, Count = count, History = history };
        fixture.Setup();
        Assert.Equal(BitConverter.DoubleToInt64Bits(fixture.Legacy()),
            BitConverter.DoubleToInt64Bits(fixture.Shared()));
    }
}
