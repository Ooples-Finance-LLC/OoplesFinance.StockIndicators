using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SeededAtrBenchmarkTests
{
    [Theory]
    [InlineData("Skender.GetAtr")]
    [InlineData("TaLib.Functions.Atr")]
    [InlineData("TaLib.Functions.Natr")]
    [InlineData("Trady.Indicator.AverageTrueRange")]
    public void BothHistoryModesAndNativePrefixesMatchTheirIndependentReferences(string operation)
    {
        foreach (int count in new[] { 64, 513, 1000, 10000 })
        {
            var benchmark = new SharedSeededAtrBenchmarks { Operation = operation, Count = count };
            try { benchmark.Setup(); }
            finally { benchmark.Cleanup(); }
        }
    }
}
