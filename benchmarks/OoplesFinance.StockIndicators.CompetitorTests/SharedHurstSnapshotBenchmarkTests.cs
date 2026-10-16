using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedHurstSnapshotBenchmarkTests
{
    [Theory]
    [InlineData(0)] [InlineData(20)] [InlineData(21)] [InlineData(1000)] [InlineData(10000)]
    public void SavedBaselineInputsPreserveBothExponentsAndPresence(int count)
    {
        var fixture = new SharedHurstSnapshotBenchmarks { Count = count };
        fixture.Setup();
        Assert.Equal(count, fixture.Snapshot().Count);
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedHurstSnapshotBenchmarks))));
    }
}
