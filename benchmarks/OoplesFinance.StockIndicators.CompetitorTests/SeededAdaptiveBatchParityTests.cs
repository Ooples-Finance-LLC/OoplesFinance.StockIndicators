using OoplesFinance.StockIndicators.CompetitorBenchmarks;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SeededAdaptiveBatchParityTests
{
    [Theory]
    [InlineData("QuanTAlib.Kama")]
    [InlineData("Skender.GetKama")]
    [InlineData("TaLib.Functions.Kama")]
    [InlineData("Trady.Indicator.KaufmanAdaptiveMovingAverage")]
    public void ActualAdapterFixturesRetainEveryColumnAndPresence(string id)
    {
        var pair = ComparisonPairs.Get(id);
        foreach (int count in new[] { 1000, 8192, 10000 })
            ComparisonVerifier.Check(pair, ComparisonVerifier.BenchmarkFixture(pair, count), 20, verifyIsolation: false);
    }
}
