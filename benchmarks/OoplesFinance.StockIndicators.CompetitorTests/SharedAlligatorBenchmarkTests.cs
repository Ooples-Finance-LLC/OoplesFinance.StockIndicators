using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedAlligatorBenchmarkTests
{
    [Theory]
    [InlineData("Skender.GetAlligator", 1000)] [InlineData("Skender.GetAlligator", 10000)]
    [InlineData("Skender.GetGator", 1000)] [InlineData("Skender.GetGator", 10000)]
    public void SavedBaselineInputsRetainEveryValueAndPresence(string operation, int count)
    {
        var fixture = new SharedAlligatorBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedAlligatorBenchmarks))));
            using var full = fixture.BuilderFull(); using var latest = fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
