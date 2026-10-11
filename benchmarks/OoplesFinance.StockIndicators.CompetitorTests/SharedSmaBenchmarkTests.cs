using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedSmaBenchmarkTests
{
    [Theory]
    [InlineData("QuanTAlib.Sma")]
    [InlineData("Skender.GetSma")]
    [InlineData("TaLib.Functions.Sma")]
    [InlineData("Trady.Indicator.SimpleMovingAverage")]
    public void BothBuilderModesAndNativeArmsMatchIndependentOracle(string operation)
    {
        foreach (int count in new[] { 20, 21, 511, 512, 513, 1000, 10000 })
        {
            var benchmark = new SharedSmaBenchmarks { Operation = operation, Count = count };
            try { benchmark.Setup(); }
            finally { benchmark.Cleanup(); }
        }
    }
}
