using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderIchimokuNativeBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetIchimoku")] public string Operation { get; set; } = "Skender.GetIchimoku";
    private SharedIchimokuSnapshotBenchmarks _owned = null!;
    private List<Quote> _quotes = null!;
    private IEnumerable<Quote>[] _inputs = null!;
    private int[] _ends = null!, _seeds = null!, _inputEnds = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        ArgumentOutOfRangeException.ThrowIfNegative(Count);
        if (Operation != "Skender.GetIchimoku") throw new ArgumentException("Unknown Ichimoku mapping.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _owned = new SharedIchimokuSnapshotBenchmarks { Count = Count, Operation = Operation };
        _owned.Setup();
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _quotes = data.Quotes;
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        // Leading B at i uses the52-bar window ending at i-26 (77
        // preceding bars); lagging output at i needs close[i+26].
        _seeds = _ends.Select((_, i) => Math.Max(0, (i == 0 ? 0 : _ends[i - 1]) - 77)).ToArray();
        _inputEnds = _ends.Select(end => Math.Min(Count, end + 26)).ToArray();
        _inputs = _inputEnds.Select((end, i) => _quotes.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        var expected = pair.CompetitorReference!(data, 20);
        foreach (var rows in new IEnumerable<IchimokuResult>[] { NativeSingle(), NativeParallel() })
        {
            ComparisonVerifier.Compare(expected, Normalize(rows), Operation, pair.ErrorBudget);
            if (!rows.Select(r => r.Date).SequenceEqual(_quotes.Select(q => q.Date)))
                throw new InvalidOperationException("Native Ichimoku dates differ.");
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public object Snapshot() => _owned.Snapshot();
    [Benchmark(Baseline = true)] public List<IchimokuResult> NativeSingle() => (List<IchimokuResult>)_quotes.GetIchimoku(9, 26, 52);
    [Benchmark] public IchimokuResult[] NativeParallel()
    {
        var result = new IchimokuResult[Count];
        if (_ends.Length == 0) return result;
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int start = worker == 0 ? 0 : _ends[worker - 1], end = _ends[worker];
            var rows = (List<IchimokuResult>)_inputs[worker].GetIchimoku(9, 26, 52);
            if (rows.Count != _inputEnds[worker] - _seeds[worker]) throw new InvalidOperationException("Native Ichimoku range differs.");
            rows.CopyTo(start - _seeds[worker], result, start, end - start);
        });
        return result;
    }
    internal static ComparisonSeries Normalize(IEnumerable<IchimokuResult> rows) =>
        IchimokuCloudComparison.NativeSeries(rows);
}
