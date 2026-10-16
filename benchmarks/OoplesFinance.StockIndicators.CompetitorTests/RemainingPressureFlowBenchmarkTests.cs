using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RemainingPressureFlowBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Kvo", "KlingerVolumeOscillator", "UltimateOscillator" }
        from count in new[] { 1_000, 10_000 }
        from history in Enum.GetValues<IndicatorHistoryMode>()
        where count == 1_000 || operation == "UltimateOscillator"
        select new object[] { operation, count, history };

    [Theory, MemberData(nameof(Cases))]
    public void BothMeasuredArmsPreserveEveryOutputBit(string operation, int count, IndicatorHistoryMode history)
        => Verify(operation, count, history);

    // Hosted contracts are partitioned by method. Keep the four expensive exact
    // recurrences independently schedulable without reducing their input sizes.
    [Fact]
    public void KlingerFull10000() => Verify("KlingerVolumeOscillator", 10_000, IndicatorHistoryMode.Full);

    [Fact]
    public void KlingerLatest10000() => Verify("KlingerVolumeOscillator", 10_000, IndicatorHistoryMode.LatestOnly);

    [Fact]
    public void KvoFull10000() => Verify("Kvo", 10_000, IndicatorHistoryMode.Full);

    [Fact]
    public void KvoLatest10000() => Verify("Kvo", 10_000, IndicatorHistoryMode.LatestOnly);

    private static void Verify(string operation, int count, IndicatorHistoryMode history)
    {
        var fixture = new RemainingPressureFlowBenchmarks { Operation = operation, Count = count, History = history };
        fixture.Setup();
        Assert.Equal(BitConverter.DoubleToInt64Bits(fixture.Legacy()),
            BitConverter.DoubleToInt64Bits(fixture.Shared()));
    }
}
