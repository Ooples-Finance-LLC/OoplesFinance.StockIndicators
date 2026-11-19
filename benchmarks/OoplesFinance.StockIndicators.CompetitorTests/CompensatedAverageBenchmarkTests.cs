using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class CompensatedAverageBenchmarkTests
{
    [Theory]
    [InlineData("QuanTAlib.Dema", 1000)]
    [InlineData("QuanTAlib.Dema", 10000)]
    [InlineData("QuanTAlib.Tema", 1000)]
    [InlineData("QuanTAlib.Tema", 10000)]
    public void LongHistoriesPreserveIndependentReferences(string operation, int count)
    {
        var benchmark = new SharedNativeEmaBenchmarks { Operation = operation, Count = count };
        try
        {
            // Setup checks both history modes bitwise against the independent
            // compensated reference, and validates both native execution arms.
            benchmark.Setup();
        }
        finally
        {
            benchmark.Cleanup();
        }
    }
}
