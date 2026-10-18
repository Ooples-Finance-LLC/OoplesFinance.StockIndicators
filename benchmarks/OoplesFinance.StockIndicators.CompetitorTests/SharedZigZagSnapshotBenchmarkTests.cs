using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedZigZagSnapshotBenchmarkTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(21)] [InlineData(1000)] [InlineData(10000)]
    public void SavedInputsPreserveEveryNullableField(int count)
    {
        var fixture = new SharedZigZagSnapshotBenchmarks { Count = count };
        fixture.Setup(); Assert.Equal(count, fixture.Snapshot().Count);
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedZigZagSnapshotBenchmarks))));
    }
}
