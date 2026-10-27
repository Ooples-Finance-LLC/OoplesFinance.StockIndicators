using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class MassNormalizedEmaBenchmarkTests
{
    [Theory]
    [InlineData(false, 1000)]
    [InlineData(false, 10000)]
    [InlineData(true, 1000)]
    [InlineData(true, 10000)]
    public void ConstructorsAndBothExecutionArmsMatchIndependentReferences(bool explicitWeight, int count)
    {
        var benchmark = new SharedMassNormalizedEmaBenchmarks { ExplicitWeight = explicitWeight, Count = count };
        try
        {
            benchmark.Setup();
        }
        finally
        {
            benchmark.Cleanup();
        }
    }
}
