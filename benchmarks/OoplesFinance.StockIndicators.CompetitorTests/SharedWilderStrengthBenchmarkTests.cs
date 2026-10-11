using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedWilderStrengthBenchmarkTests
{
    [Theory]
    [InlineData("TaLib.Functions.Rsi", 21)]
    [InlineData("TaLib.Functions.Rsi", 65)]
    [InlineData("TaLib.Functions.Cmo", 21)]
    [InlineData("TaLib.Functions.Cmo", 65)]
    public void SetupVerifiesBothOwnedHistoriesAndNativePrefixReplay(string operation, int count)
    {
        var fixture = new SharedWilderStrengthBenchmarks { Operation = operation, Count = count };
        try { fixture.Setup(); }
        finally { fixture.Cleanup(); }
    }
}
