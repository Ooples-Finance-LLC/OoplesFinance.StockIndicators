using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedWilderStrengthBenchmarkTests
{
    [Theory]
    [InlineData("TaLib.Functions.Rsi", 21)]
    [InlineData("TaLib.Functions.Rsi", 65)]
    [InlineData("TaLib.Functions.Cmo", 21)]
    [InlineData("TaLib.Functions.Cmo", 65)]
    [InlineData("TaLib.Functions.Rsi", 1000)]
    [InlineData("TaLib.Functions.Rsi", 10000)]
    [InlineData("TaLib.Functions.Cmo", 1000)]
    [InlineData("TaLib.Functions.Cmo", 10000)]
    public void SetupVerifiesBothOwnedHistoriesAndNativePrefixReplay(string operation, int count)
    {
        var fixture = new SharedWilderStrengthBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedWilderStrengthBenchmarks))));
        }
        finally { fixture.Cleanup(); }
    }
}
