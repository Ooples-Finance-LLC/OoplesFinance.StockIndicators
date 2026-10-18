using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedVolatilityStopSnapshotBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetVolatilityStop")] public string Operation { get; set; } = "Skender.GetVolatilityStop";
    private Bar[] _bars = null!;
    [GlobalSetup]
    public void Setup()
    {
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count); _bars = data.IndicatorBars;
        var rows = Snapshot();
        var actual = VolatilityStopComparison.Series([rows.Select(r => r.Sar).ToArray(),
            rows.Select(r => r.UpperBand).ToArray(), rows.Select(r => r.LowerBand).ToArray(),
            rows.Select(r => r.IsStop.HasValue ? (double?)(r.IsStop.Value ? 1 : 0) : null).ToArray()]);
        ComparisonVerifier.Compare(pair.Reference!(data, 20), actual, "Volatility stop saved-baseline fixture", IndicatorErrorBudget.Exact);
    }
    // Saved period20/multiplier3 fixture; no native setup/timing calls.
    [Benchmark] public IReadOnlyList<VolatilityStopValue> Snapshot() => VolatilityStopSnapshot.Calculate(_bars, 20);
}
