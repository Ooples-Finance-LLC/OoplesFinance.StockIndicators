using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RemainingChandeMomentumBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "ChandeMomentumOscillatorAbsolute", "ChandeMomentumOscillatorAbsoluteAverage", "ChandeMomentumOscillatorAverage", "ChandeMomentumOscillatorAverageDisparityIndex", "ChandeMomentumOscillatorFilter" }
        from count in new[] { 1_000, 10_000 }
        from history in Enum.GetValues<IndicatorHistoryMode>()
        select new object[] { operation, count, history };

    [Theory, MemberData(nameof(Cases))]
    public void BothMeasuredArmsPreserveEveryOutputBit(string operation, int count, IndicatorHistoryMode history)
    {
        var fixture = new RemainingChandeMomentumBenchmarks { Operation = operation, Count = count, History = history };
        fixture.Setup();
        Assert.Equal(BitConverter.DoubleToInt64Bits(fixture.Legacy()), BitConverter.DoubleToInt64Bits(fixture.Shared()));
    }
}
