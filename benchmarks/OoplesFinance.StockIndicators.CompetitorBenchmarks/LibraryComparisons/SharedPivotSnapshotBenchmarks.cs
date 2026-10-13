using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedPivotSnapshotBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetPivotPoints", "Skender.GetPivots")] public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    [GlobalSetup]
    public void Setup()
    {
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count); _bars = data.IndicatorBars;
        var snapshot = Snapshot();
        ComparisonSeries actual;
        if (snapshot is IReadOnlyList<PivotTrendValue> t)
            actual = PivotTrendComparison.Series([
                t.Select(r => r.HighPoint).ToArray(), t.Select(r => r.LowPoint).ToArray(),
                t.Select(r => r.HighLine).ToArray(), t.Select(r => r.LowLine).ToArray(),
                t.Select(r => r.HighTrend.HasValue ? (double?)(int)r.HighTrend.Value : null).ToArray(),
                t.Select(r => r.LowTrend.HasValue ? (double?)(int)r.LowTrend.Value : null).ToArray()]);
        else
        {
            var p = (IReadOnlyList<PivotLevelValue>)snapshot;
            actual = PivotLevelComparison.Series([p.Select(r => r.PP).ToArray(),
                p.Select(r => r.S1).ToArray(), p.Select(r => r.S2).ToArray(), p.Select(r => r.S3).ToArray(), p.Select(r => r.S4).ToArray(),
                p.Select(r => r.R1).ToArray(), p.Select(r => r.R2).ToArray(), p.Select(r => r.R3).ToArray(), p.Select(r => r.R4).ToArray()]);
        }
        ComparisonVerifier.Compare(pair.Reference!(data, 20), actual, "Pivot saved-baseline fixture", IndicatorErrorBudget.Exact);
    }
    // Match saved calendar-hour/standard and trend2/2/20 defaults. No native calls.
    [Benchmark]
    public object Snapshot() => Operation switch
    {
        "Skender.GetPivotPoints" => PivotLevelSnapshots.Calendar(_bars, PivotCalendarWindow.Hour),
        "Skender.GetPivots" => PivotTrendSnapshot.Calculate(_bars),
        _ => throw new ArgumentException("Unknown pivot snapshot.")
    };
}
