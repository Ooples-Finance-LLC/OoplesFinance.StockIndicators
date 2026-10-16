using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using Trady.Analysis.Indicator;
using Cloud = (decimal? ConversionLine, decimal? BaseLine, decimal? LeadingSpanA, decimal? LeadingSpanB, decimal? LaggingSpan);

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyIchimokuNativeBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Trady.Indicator.IchimokuCloud")] public string Operation { get; set; } = "Trady.Indicator.IchimokuCloud";
    private SharedIchimokuSnapshotBenchmarks _owned = null!;
    private (decimal High, decimal Low, decimal Close)[] _prices = null!;
    private int[][] _indexes = null!;
    private int[] _ends = null!;
    private int _priorDop;
    private int OutputCount => checked(Count + 51);

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        ArgumentOutOfRangeException.ThrowIfNegative(Count);
        if (Operation != "Trady.Indicator.IchimokuCloud") throw new ArgumentException("Unknown Trady cloud mapping.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _owned = new SharedIchimokuSnapshotBenchmarks { Count = Count, Operation = Operation };
        _owned.Setup();
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _prices = data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
        int workers = Math.Min(OutputCount, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)OutputCount * i / workers)).ToArray();
        _indexes = _ends.Select((end, i) =>
        {
            int start = i == 0 ? 0 : _ends[i - 1];
            return Enumerable.Range(start - 25, end - start).ToArray();
        }).ToArray();
        var expected = pair.CompetitorReference!(data, 20);
        foreach (var rows in new IReadOnlyList<Cloud>[] { NativeSingle(), NativeParallel() })
        {
            if (rows.Count != OutputCount) throw new InvalidOperationException("Extended cloud row count differs.");
            ComparisonVerifier.Compare(expected, Normalize(rows), Operation, pair.ErrorBudget);
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public object Snapshot() => _owned.Snapshot();
    [Benchmark(Baseline = true)] public IReadOnlyList<Cloud> NativeSingle() => new IchimokuCloudByTuple(_prices, 9, 26, 52).Compute();
    [Benchmark] public Cloud[] NativeParallel()
    {
        var output = new Cloud[OutputCount];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int start = worker == 0 ? 0 : _ends[worker - 1], end = _ends[worker];
            // The range overload expands its bounds; int[] selects a single
            // result overload. IEnumerable<int> requests these exact coordinates.
            var rows = new IchimokuCloudByTuple(_prices, 9, 26, 52).Compute((IEnumerable<int>)_indexes[worker]);
            if (rows.Count != end - start) throw new InvalidOperationException("Native cloud index range differs.");
            for (int i = 0; i < rows.Count; i++) output[start + i] = rows[i];
        });
        return output;
    }
    internal static ComparisonSeries Normalize(IEnumerable<Cloud> source)
    {
        var rows = source.ToArray();
        return ExtendedCloudComparison.Series([
            rows.Select(r => (double?)r.ConversionLine).ToArray(), rows.Select(r => (double?)r.BaseLine).ToArray(),
            rows.Select(r => (double?)r.LeadingSpanA).ToArray(), rows.Select(r => (double?)r.LeadingSpanB).ToArray(),
            rows.Select(r => (double?)r.LaggingSpan).ToArray()]);
    }
}
