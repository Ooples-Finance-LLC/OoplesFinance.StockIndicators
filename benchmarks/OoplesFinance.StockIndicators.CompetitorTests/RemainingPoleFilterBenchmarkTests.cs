using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RemainingPoleFilterBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Ehlers2PoleButterworthFilterV1", "Ehlers2PoleButterworthFilterV2", "Ehlers2PoleSuperSmootherFilterV1", "Ehlers2PoleSuperSmootherFilterV2", "Ehlers3PoleButterworthFilterV1", "Ehlers3PoleButterworthFilterV2", "Ehlers3PoleSuperSmootherFilter" }
        from count in new[] { 1_000, 10_000 }
        from history in Enum.GetValues<IndicatorHistoryMode>()
        select new object[] { operation, count, history };

    [Theory, MemberData(nameof(Cases))]
    public void BothMeasuredArmsPreserveEveryOutputBit(string operation, int count, IndicatorHistoryMode history)
    {
        var fixture = new RemainingPoleFilterBenchmarks { Operation = operation, Count = count, History = history };
        fixture.Setup();
        Assert.Equal(BitConverter.DoubleToInt64Bits(fixture.Legacy()),
            BitConverter.DoubleToInt64Bits(fixture.Shared()));
    }
}
