using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedRenkoSnapshotBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetRenko", "Skender.GetRenkoAtr" }
        from count in new[] { 0, 1, 20, 21, 1000, 10000 }
        select new object[] { operation, count };
    [Theory, MemberData(nameof(Cases))]
    public void SavedInputsPreserveEveryMaterializedBrickField(string operation, int count)
    {
        var fixture = new SharedRenkoSnapshotBenchmarks { Operation = operation, Count = count };
        fixture.Setup();
        Assert.Equal(fixture.Snapshot(), fixture.Snapshot());
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedRenkoSnapshotBenchmarks))));
    }

    [Fact]
    public void FixedIteratorRetainsDeferredInputReadsAndMaterializedRowsAreOwned()
    {
        Bar[] bars = [new(DateTime.UnixEpoch, 10, 10, 10, 10, 0),
            new(DateTime.UnixEpoch.AddDays(1), 10, 12, 10, 12, 8)];
        var deferred = RenkoSnapshot.Calculate(bars);
        bars[1] = new(bars[1].Time, 10, 14, 10, 14, 8);
        var materialized = deferred.ToArray();
        Assert.Equal(4, materialized.Length);
        Assert.All(materialized, row => Assert.Equal(2, row.Volume));
        Assert.Equal(14, materialized[^1].Close);
        bars[1] = new(bars[1].Time, 10, 11, 10, 11, 8);
        Assert.Single(deferred);
        Assert.Equal(4, materialized.Length);
        Assert.Equal(14, materialized[^1].Close);
    }

    [Fact]
    public void AtrSizeIsCapturedAtCallWhileBricksRemainDeferred()
    {
        var bars = CompetitorData.Create(80).IndicatorBars;
        var size = SeededAtrComparison.Reference(CompetitorData.Create(80), 20, false, 0)
            .Outputs.Values.Single().Values[^1];
        var deferred = RenkoSnapshot.CalculateAtr(bars, 20);
        var last = bars[^1];
        bars[^1] = new(last.Time, last.Open, last.High + 10, last.Low, last.Close + 10, last.Volume);
        Assert.Equal(RenkoSnapshot.Calculate(bars, size).ToArray(), deferred.ToArray());
    }
}
