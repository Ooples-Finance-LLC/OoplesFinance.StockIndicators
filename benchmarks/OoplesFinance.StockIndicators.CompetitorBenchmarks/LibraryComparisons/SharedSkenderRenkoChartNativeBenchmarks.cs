using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderRenkoChartNativeBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetRenko", "Skender.GetRenkoAtr")] public string Operation { get; set; } = "";
    private SharedRenkoSnapshotBenchmarks _owned = null!;
    private List<Quote> _quotes = null!;
    private int[] _ends = null!;
    private bool _atr;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        ArgumentOutOfRangeException.ThrowIfNegative(Count);
        if (Operation != "Skender.GetRenko" && Operation != "Skender.GetRenkoAtr")
            throw new ArgumentException("Unknown Renko mapping.");
        _atr = Operation == "Skender.GetRenkoAtr";
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _owned = new SharedRenkoSnapshotBenchmarks { Count = Count, Operation = Operation };
        _owned.Setup();
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _quotes = data.Quotes;
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        var expected = pair.CompetitorReference!(data, 20);
        foreach (var rows in new IEnumerable<RenkoResult>[] { NativeSingle(), NativeParallel() })
            ComparisonVerifier.Compare(expected, Normalize(rows), Operation, pair.ErrorBudget);
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public RenkoBrick[] Snapshot() => _owned.Snapshot();
    [Benchmark(Baseline = true)] public List<RenkoResult> NativeSingle() =>
        (List<RenkoResult>)(_atr ? _quotes.GetRenkoAtr(20, EndType.Close) : _quotes.GetRenko(1m, EndType.Close));
    [Benchmark] public RenkoResult[] NativeParallel() => CalculateParallel(_quotes, _ends, _atr);

    internal static RenkoResult[] CalculateParallel(List<Quote> quotes, int[] ends, bool atr)
    {
        if (ends.Length == 0) return [];
        // Match GetRenkoAtr: the final ATR sets one size for the entire chart.
        // This calculation and conversion stay inside every timed invocation.
        decimal size = atr ? (decimal)(quotes.GetAtr(20).LastOrDefault()?.Atr ?? 0) : 1m;
        if (size == 0) return [];
        var chunks = new List<RenkoResult>[ends.Length];
        CpuParallelSettings.LightweightParallel(ends.Length, ends.Length, worker =>
        {
            int start = worker == 0 ? 0 : ends[worker - 1];
            var rows = (List<RenkoResult>)quotes.Take(ends[worker]).GetRenko(size, EndType.Close);
            // Keep all bricks from each assigned input bar, including multiple
            // bricks sharing a timestamp and their divided native volume.
            int first = rows.FindIndex(row => row.Date >= quotes[start].Date);
            chunks[worker] = first < 0 ? [] : rows.GetRange(first, rows.Count - first);
        });
        var output = new RenkoResult[chunks.Sum(chunk => chunk.Count)];
        int offset = 0;
        foreach (var chunk in chunks)
        {
            chunk.CopyTo(output, offset);
            offset += chunk.Count;
        }
        return output;
    }
    internal static ComparisonSeries Normalize(IEnumerable<RenkoResult> rows) =>
        RenkoComparison.Series(rows.Select(r => new RenkoBrick(r.Date, (double)r.Open, (double)r.High,
            (double)r.Low, (double)r.Close, (double)r.Volume, r.IsUp)));
}
