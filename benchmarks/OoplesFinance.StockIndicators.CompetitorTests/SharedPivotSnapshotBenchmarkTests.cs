using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedPivotSnapshotBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetPivotPoints", "Skender.GetPivots" }
        from count in new[] { 0, 4, 5, 21, 1000, 10000 }
        select new object[] { operation, count };
    [Theory, MemberData(nameof(Cases))]
    public void SavedInputsPreserveEveryNullableField(string operation, int count)
    {
        var fixture = new SharedPivotSnapshotBenchmarks { Operation = operation, Count = count };
        fixture.Setup(); Assert.NotNull(fixture.Snapshot());
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedPivotSnapshotBenchmarks))));
    }
}
