using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedRetrospectiveSnapshotBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetDpo", "Skender.GetFcb" }
        from count in new[] { 0, 20, 41, 1000, 10000 }
        select new object[] { operation, count };
    [Theory, MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainEveryNullableSnapshotField(string operation, int count)
    {
        var fixture = new SharedRetrospectiveSnapshotBenchmarks { Operation = operation, Count = count };
        fixture.Setup();
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedRetrospectiveSnapshotBenchmarks))));
        Assert.NotNull(fixture.Snapshot());
    }
}
