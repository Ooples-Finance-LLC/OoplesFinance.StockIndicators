using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedParabolicSnapshotBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "TaLib.Functions.Sar", "TaLib.Functions.SarExt", "Skender.GetParabolicSar", "Trady.Indicator.ParabolicStopAndReverse" }
        from count in new[] { 0, 1, 4, 5, 6, 1000, 10000 }
        select new object[] { operation, count };
    [Theory, MemberData(nameof(Cases))]
    public void SavedBaselineInputsPreserveStopsAndReversals(string operation, int count)
    {
        var fixture = new SharedParabolicSnapshotBenchmarks { Count = count, Operation = operation };
        fixture.Setup();
        Assert.NotNull(fixture.Snapshot());
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedParabolicSnapshotBenchmarks))));
    }
}
