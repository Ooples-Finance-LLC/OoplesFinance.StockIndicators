using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedParabolicSnapshotBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("TaLib.Functions.Sar", "TaLib.Functions.SarExt", "Skender.GetParabolicSar", "Trady.Indicator.ParabolicStopAndReverse")]
    public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private int _kind;
    [GlobalSetup]
    public void Setup()
    {
        _kind = Array.IndexOf(ParabolicComparison.Ids, Operation);
        if (_kind < 0) throw new ArgumentException("Unknown parabolic snapshot.");
        var pair = ParabolicComparison.Pair(_kind);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _bars = data.IndicatorBars;
        var snapshot = Snapshot();
        double?[][] columns;
        if (snapshot is IReadOnlyList<ConfirmedParabolicValue> confirmed)
            columns = [confirmed.Select(r => r.Sar).ToArray(), confirmed.Select(r => r.IsReversal.HasValue ? (double?)(r.IsReversal.Value ? 1 : 0) : null).ToArray()];
        else columns = [((IReadOnlyList<double?>)snapshot).ToArray()];
        ComparisonVerifier.Compare(pair.Reference!(data, 20), ParabolicComparison.Series(columns, _kind),
            "Parabolic saved-baseline fixture", IndicatorErrorBudget.Exact);
    }
    // Saved native defaults/input generator; no native setup or timing calls.
    [Benchmark]
    public object Snapshot() => _kind switch
    {
        0 => ParabolicStopSnapshots.Classic(_bars),
        1 => ParabolicStopSnapshots.Extended(_bars),
        2 => ParabolicStopSnapshots.Confirmed(_bars),
        3 => ParabolicStopSnapshots.FourBar(_bars),
        _ => throw new InvalidOperationException("Setup required.")
    };
}
