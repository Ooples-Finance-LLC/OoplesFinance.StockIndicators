using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class WilderBenchmarkTests
{
    [Theory]
    [InlineData("QuanTAlib.Smma")]
    [InlineData("Skender.GetSmma")]
    [InlineData("Trady.Indicator.ModifiedMovingAverage")]
    public void IndependentBitsPresenceAndRecursiveReplay(string operation)
    {
        var benchmark = new SharedWilderAverageBenchmarks { Operation = operation, Count = 1000 };
        try { benchmark.Setup(); }
        finally { benchmark.Cleanup(); }
    }
}
