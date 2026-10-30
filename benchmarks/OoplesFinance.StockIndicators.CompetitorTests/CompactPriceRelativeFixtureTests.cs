using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("Price relative benchmark settings")]
public sealed class CompactPriceRelativeFixtureTests
{
    [Theory]
    [InlineData(1000)]
    [InlineData(10000)]
    public void NativeDiagnosticFixtureRetainsExactReturns(int count)
    {
        var fixture = new SharedSkenderDiagnosticsBenchmarks { Operation = "Skender.GetPrs", Count = count };
        try { fixture.Setup(); }
        finally { fixture.Cleanup(); }
    }
}
