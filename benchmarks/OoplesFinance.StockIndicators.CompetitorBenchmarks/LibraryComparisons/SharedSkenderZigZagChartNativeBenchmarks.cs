using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderZigZagChartNativeBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetZigZag")] public string Operation { get; set; } = "Skender.GetZigZag";
    private SharedZigZagSnapshotBenchmarks _owned = null!;
    private List<Quote> _quotes = null!;

    [GlobalSetup]
    public void Setup()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(Count);
        if (Operation != "Skender.GetZigZag") throw new ArgumentException("Unknown ZigZag mapping.");
        _owned = new SharedZigZagSnapshotBenchmarks { Count = Count };
        _owned.Setup();
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _quotes = data.Quotes;
        var rows = NativeSingle();
        ComparisonVerifier.Compare(pair.CompetitorReference!(data, 20), Normalize(rows), Operation, pair.ErrorBudget);
        if (!rows.Select(r => r.Date).SequenceEqual(_quotes.Select(q => q.Date)))
            throw new InvalidOperationException("Native ZigZag dates differ.");
    }
    [Benchmark] public IReadOnlyList<ZigZagValue> Snapshot() => _owned.Snapshot();
    // Native sequential pivot state and unbounded future retraces prevent
    // independent finite-window partitions. Measure the original eager API.
    [Benchmark(Baseline = true)] public List<ZigZagResult> NativeSingle() =>
        (List<ZigZagResult>)_quotes.GetZigZag(EndType.Close, 5m);
    internal static ComparisonSeries Normalize(IEnumerable<ZigZagResult> rows) =>
        ZigZagComparison.Series(rows.Select(r => new ZigZagValue((double?)r.ZigZag,
            r.PointType == "H" ? ZigZagPointKind.High : r.PointType == "L" ? ZigZagPointKind.Low : null,
            (double?)r.RetraceHigh, (double?)r.RetraceLow)));
}
