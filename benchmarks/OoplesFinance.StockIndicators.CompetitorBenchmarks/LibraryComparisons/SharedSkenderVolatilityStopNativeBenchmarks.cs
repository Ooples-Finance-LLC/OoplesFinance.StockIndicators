using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderVolatilityStopNativeBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetVolatilityStop")] public string Operation { get; set; } = "Skender.GetVolatilityStop";
    private SharedVolatilityStopSnapshotBenchmarks _owned = null!;
    private List<Quote> _quotes = null!;
    private int[] _ends = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        ArgumentOutOfRangeException.ThrowIfNegative(Count);
        if (Operation != "Skender.GetVolatilityStop") throw new ArgumentException("Unknown volatility stop mapping.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _owned = new SharedVolatilityStopSnapshotBenchmarks { Count = Count };
        _owned.Setup();
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _quotes = data.Quotes;
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        var expected = pair.CompetitorReference!(data, 20);
        foreach (var rows in new IEnumerable<VolatilityStopResult>[] { NativeSingle(), NativeParallel() })
        {
            ComparisonVerifier.Compare(expected, Normalize(rows), Operation, pair.ErrorBudget);
            if (!rows.Select(r => r.Date).SequenceEqual(_quotes.Select(q => q.Date)))
                throw new InvalidOperationException("Native volatility stop dates differ.");
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IReadOnlyList<VolatilityStopValue> Snapshot() => _owned.Snapshot();
    [Benchmark(Baseline = true)] public List<VolatilityStopResult> NativeSingle() => (List<VolatilityStopResult>)_quotes.GetVolatilityStop(20, 3);
    [Benchmark] public VolatilityStopResult[] NativeParallel()
    {
        var result = new VolatilityStopResult[Count];
        if (_ends.Length == 0) return result;
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int start = worker == 0 ? 0 : _ends[worker - 1], end = _ends[worker];
            var rows = PrefixWithFinalStartup(_quotes, end);
            rows.CopyTo(start, result, start, end - start);
        });
        return result;
    }
    internal static List<VolatilityStopResult> PrefixWithFinalStartup(List<Quote> quotes, int end)
    {
        var rows = (List<VolatilityStopResult>)quotes.Take(end).GetVolatilityStop(20, 3);
        // At period20 IsStop is false until the first reversal retrospectively
        // clears it. If still present, later input may invalidate startup rows.
        // Outputs before period20 are null under either outcome.
        if (end > 20 && end < quotes.Count && rows[20].IsStop.HasValue)
            rows = (List<VolatilityStopResult>)quotes.GetVolatilityStop(20, 3);
        return rows;
    }
    internal static ComparisonSeries Normalize(IEnumerable<VolatilityStopResult> source)
    {
        var rows = source.ToArray();
        return VolatilityStopComparison.Series([rows.Select(r => r.Sar).ToArray(),
            rows.Select(r => r.UpperBand).ToArray(), rows.Select(r => r.LowerBand).ToArray(),
            rows.Select(r => r.IsStop.HasValue ? (double?)(r.IsStop.Value ? 1 : 0) : null).ToArray()]);
    }
}
