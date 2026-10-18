using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedRocBenchmarkTests
{
    [Theory]
    [InlineData("Skender.GetRoc", 1000)]
    [InlineData("Skender.GetRoc", 10000)]
    [InlineData("Skender.GetRocWb", 1000)]
    [InlineData("Skender.GetRocWb", 10000)]
    public void SavedBaselineInputsRetainEveryValueAndPresence(string operation, int count)
    {
        var fixture = new SharedRocBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedRocBenchmarks))));
            using var full = fixture.BuilderFull();
            using var latest = fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
