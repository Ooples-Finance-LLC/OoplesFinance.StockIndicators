using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedVolumeIndexBenchmarkTests
{
    [Theory]
    [InlineData("Trady.Indicator.PositiveVolumeIndex", 1000)]
    [InlineData("Trady.Indicator.PositiveVolumeIndex", 10000)]
    [InlineData("Trady.Indicator.NegativeVolumeIndex", 1000)]
    [InlineData("Trady.Indicator.NegativeVolumeIndex", 10000)]
    public void SavedBaselineFixtureRetainsExactValuesAndPresence(string operation, int count)
    {
        var fixture = new SharedVolumeIndexBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedVolumeIndexBenchmarks))));
            using var full = fixture.BuilderFull();
            using var latest = fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
