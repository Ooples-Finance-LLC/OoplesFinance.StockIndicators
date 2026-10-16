using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRenkoSnapshotBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetRenko", "Skender.GetRenkoAtr")] public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    [GlobalSetup]
    public void Setup()
    {
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count); _bars = data.IndicatorBars;
        ComparisonVerifier.Compare(pair.Reference!(data, 20), RenkoComparison.Series(Snapshot()),
            "Renko saved-baseline fixture", IndicatorErrorBudget.Exact);
    }
    // Materialize every brick inside timing. Saved defaults are fixed1/ATR20,
    // close thresholds; no native setup/timing calls and no deferred work escapes.
    [Benchmark]
    public RenkoBrick[] Snapshot() => Operation switch
    {
        "Skender.GetRenko" => RenkoSnapshot.Calculate(_bars).ToArray(),
        "Skender.GetRenkoAtr" => RenkoSnapshot.CalculateAtr(_bars, 20).ToArray(),
        _ => throw new ArgumentException("Unknown Renko snapshot.")
    };
}
