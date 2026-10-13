using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedIchimokuSnapshotBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetIchimoku", "Trady.Indicator.IchimokuCloud" }
        from count in new[] { 0, 26, 52, 1000, 10000 }
        select new object[] { operation, count };
    [Theory, MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainEveryNullableSnapshotField(string operation, int count)
    {
        var fixture = new SharedIchimokuSnapshotBenchmarks { Operation = operation, Count = count };
        fixture.Setup();
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedIchimokuSnapshotBenchmarks))));
        Assert.NotNull(fixture.Snapshot());
    }
}
