using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedTaDirectionalBenchmarkTests
{
    [Theory]
    [InlineData("PlusDM")]
    [InlineData("MinusDM")]
    [InlineData("PlusDI")]
    [InlineData("MinusDI")]
    [InlineData("Dx")]
    [InlineData("Adx")]
    [InlineData("Adxr")]
    public void NativePrefixesAndBuilderMasksMatchIndependentReferences(string name)
    {
        foreach (int count in new[] { 64, 513, 1000, 10000 })
        {
            var benchmark = new SharedTaDirectionalBenchmarks { Operation = "TaLib.Functions." + name, Count = count };
            try { benchmark.Setup(); }
            finally { benchmark.Cleanup(); }
        }
    }
}
