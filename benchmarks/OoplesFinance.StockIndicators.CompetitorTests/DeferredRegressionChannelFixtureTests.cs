using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("Regression channel benchmark settings")]
public sealed class DeferredRegressionChannelFixtureTests
{
    [Theory]
    [InlineData(1000)]
    [InlineData(10000)]
    public void NativeDiagnosticFixturePreservesHistoricalChannels(int count)
    {
        var fixture = new SharedSkenderDiagnosticsBenchmarks { Operation = "Skender.GetStdDevChannels", Count = count };
        try { fixture.Setup(); }
        finally { fixture.Cleanup(); }
    }
}
