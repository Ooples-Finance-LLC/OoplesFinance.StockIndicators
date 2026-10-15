using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderHurstNativeBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetHurst")] public string Operation { get; set; } = "Skender.GetHurst";
    private SharedHurstSnapshotBenchmarks _owned = null!;
    private List<Quote> _quotes = null!;
    private IEnumerable<Quote>[] _inputs = null!;
    private int[] _ends = null!, _seeds = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        ArgumentOutOfRangeException.ThrowIfNegative(Count);
        if (Operation != "Skender.GetHurst") throw new ArgumentException("Unknown Hurst mapping.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _owned = new SharedHurstSnapshotBenchmarks { Count = Count };
        _owned.Setup();
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _quotes = data.Quotes;
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        // Twenty returns need twenty preceding closes, including the first
        // return's denominator. Each Hurst regression is window-local.
        _seeds = _ends.Select((_, i) => Math.Max(0, (i == 0 ? 0 : _ends[i - 1]) - 20)).ToArray();
        _inputs = _ends.Select((end, i) => _quotes.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        var expected = pair.CompetitorReference!(data, 20);
        foreach (var rows in new IEnumerable<HurstResult>[] { NativeSingle(), NativeParallel() })
        {
            ComparisonVerifier.Compare(expected, Normalize(rows), Operation, HurstComparison.Budget);
            if (!rows.Select(r => r.Date).SequenceEqual(_quotes.Select(q => q.Date)))
                throw new InvalidOperationException("Native Hurst dates differ.");
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IReadOnlyList<HurstValue> Snapshot() => _owned.Snapshot();
    [Benchmark(Baseline = true)] public List<HurstResult> NativeSingle() => (List<HurstResult>)_quotes.GetHurst(20);
    [Benchmark] public HurstResult[] NativeParallel()
    {
        var result = new HurstResult[Count];
        if (_ends.Length == 0) return result;
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int start = worker == 0 ? 0 : _ends[worker - 1], end = _ends[worker];
            var rows = (List<HurstResult>)_inputs[worker].GetHurst(20);
            if (rows.Count != end - _seeds[worker]) throw new InvalidOperationException("Native Hurst range differs.");
            rows.CopyTo(start - _seeds[worker], result, start, end - start);
        });
        return result;
    }
    internal static ComparisonSeries Normalize(IEnumerable<HurstResult> rows) =>
        HurstComparison.Series(rows.Select(r => new HurstValue(r.HurstExponent, r.HurstExponentAL)));
}
