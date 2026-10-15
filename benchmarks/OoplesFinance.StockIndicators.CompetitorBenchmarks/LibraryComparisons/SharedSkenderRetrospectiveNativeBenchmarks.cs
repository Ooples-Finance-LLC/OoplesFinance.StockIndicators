using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderRetrospectiveNativeBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetDpo", "Skender.GetFcb")] public string Operation { get; set; } = "";
    private SharedRetrospectiveSnapshotBenchmarks _owned = null!;
    private List<Quote> _quotes = null!;
    private IEnumerable<Quote>[] _inputs = null!;
    private int[] _ends = null!, _inputEnds = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        ArgumentOutOfRangeException.ThrowIfNegative(Count);
        if (Operation is not ("Skender.GetDpo" or "Skender.GetFcb")) throw new ArgumentException("Unknown retrospective mapping.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _owned = new SharedRetrospectiveSnapshotBenchmarks { Operation = Operation, Count = Count };
        _owned.Setup();
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _quotes = data.Quotes;
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        // DPO shifts its SMA eleven bars back. FCB publishes only confirmed
        // fractals, so its carried bands need the full prefix but no future bars.
        int future = Operation == "Skender.GetDpo" ? 11 : 0;
        _inputEnds = _ends.Select(end => Math.Min(Count, end + future)).ToArray();
        _inputs = _inputEnds.Select(end => _quotes.Take(end)).ToArray();
        var expected = pair.CompetitorReference!(data, 20);
        foreach (var result in new[] { NativeSingle(), NativeParallel() })
        {
            ComparisonVerifier.Compare(expected, Normalize(result), Operation + " direct native", pair.ErrorBudget);
            if (!((IEnumerable<ResultBase>)result).Select(r => r.Date).SequenceEqual(_quotes.Select(q => q.Date)))
                throw new InvalidOperationException("Native retrospective dates differ.");
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public object Snapshot() => _owned.Snapshot();
    [Benchmark(Baseline = true)] public object NativeSingle() => Operation == "Skender.GetDpo" ? _quotes.GetDpo(20) : (object)_quotes.GetFcb(20);
    [Benchmark] public object NativeParallel() => Operation == "Skender.GetDpo"
        ? Parallel(static q => q.GetDpo(20)) : (object)Parallel(static q => q.GetFcb(20));

    private T[] Parallel<T>(Func<IEnumerable<Quote>, IEnumerable<T>> calculate)
    {
        var output = new T[Count];
        if (_ends.Length == 0) return output;
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int start = worker == 0 ? 0 : _ends[worker - 1], end = _ends[worker];
            var rows = (List<T>)calculate(_inputs[worker]);
            if (rows.Count != _inputEnds[worker]) throw new InvalidOperationException("Native retrospective row count differs.");
            rows.CopyTo(start, output, start, end - start);
        });
        return output;
    }
    internal static ComparisonSeries Normalize(object result) => result switch
    {
        IReadOnlyList<DpoResult> rows => RetrospectivePriceComparison.Series(RetrospectivePriceComparison.DpoNames,
            [rows.Select(r => r.Dpo).ToArray(), rows.Select(r => r.Sma).ToArray()]),
        IReadOnlyList<FcbResult> rows => RetrospectivePriceComparison.Series(RetrospectivePriceComparison.ChaosNames,
            [rows.Select(r => (double?)r.UpperBand).ToArray(), rows.Select(r => (double?)r.LowerBand).ToArray()]),
        _ => throw new ArgumentException("Unexpected retrospective native result.")
    };
}
