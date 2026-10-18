using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SeededEmaBenchmarkTests
{
    [Theory]
    [InlineData("Skender.GetDema")]
    [InlineData("Skender.GetTema")]
    [InlineData("TaLib.Functions.Dema")]
    [InlineData("TaLib.Functions.Tema")]
    public void IndependentBitsPresenceAndFullPrefixParity(string operation)
    {
        var benchmark = new SharedSeededEmaBenchmarks { Operation = operation, Count = 1000 };
        try { benchmark.Setup(); }
        finally { benchmark.Cleanup(); }
    }
}
