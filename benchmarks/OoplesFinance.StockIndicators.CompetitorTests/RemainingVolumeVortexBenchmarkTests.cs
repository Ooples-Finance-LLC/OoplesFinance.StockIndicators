using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RemainingVolumeVortexBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Vpci", "VolumeWeightedRsi", "VortexBands" }
        from count in new[] { 1_000, 10_000 }
        from history in Enum.GetValues<IndicatorHistoryMode>()
        where operation != "VortexBands" || count != 10_000
        select new object[] { operation, count, history };

    [Theory, MemberData(nameof(Cases))]
    public void BothMeasuredArmsPreserveEveryOutputBit(string operation, int count, IndicatorHistoryMode history)
        => Verify(operation, count, history);

    // Each long exact recurrence must be independently assignable to a hosted runner.
    [Fact]
    public void VortexFull10000() => Verify("VortexBands", 10_000, IndicatorHistoryMode.Full);

    [Fact]
    public void VortexLatest10000() => Verify("VortexBands", 10_000, IndicatorHistoryMode.LatestOnly);

    private static void Verify(string operation, int count, IndicatorHistoryMode history)
    {
        var fixture = new RemainingVolumeVortexBenchmarks { Operation = operation, Count = count, History = history };
        fixture.Setup();
        Assert.Equal(BitConverter.DoubleToInt64Bits(fixture.Legacy()),
            BitConverter.DoubleToInt64Bits(fixture.Shared()));
    }
}
