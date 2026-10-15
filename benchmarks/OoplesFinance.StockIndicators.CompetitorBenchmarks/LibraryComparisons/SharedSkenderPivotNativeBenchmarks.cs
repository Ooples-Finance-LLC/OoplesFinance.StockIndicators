using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderPivotNativeBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetPivotPoints", "Skender.GetPivots")] public string Operation { get; set; } = "";
    private SharedPivotSnapshotBenchmarks _owned = null!;
    private List<Quote> _quotes = null!;
    private IEnumerable<Quote>[] _inputs = null!;
    private int[] _ends = null!, _inputEnds = null!;
    private bool _trend;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        ArgumentOutOfRangeException.ThrowIfNegative(Count);
        if (Operation != "Skender.GetPivotPoints" && Operation != "Skender.GetPivots")
            throw new ArgumentException("Unknown pivot mapping.");
        _trend = Operation == "Skender.GetPivots";
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _owned = new SharedPivotSnapshotBenchmarks { Count = Count, Operation = Operation };
        _owned.Setup();
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _quotes = data.Quotes;
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        // A trend anchor can redraw the preceding20 bars after two right bars
        // confirm it. Calendar levels use only the previous completed period.
        _inputEnds = _ends.Select(end => (int)Math.Min(Count, (long)end + (_trend ? 22 : 0))).ToArray();
        _inputs = _inputEnds.Select(end => _quotes.Take(end)).ToArray();
        var expected = pair.CompetitorReference!(data, 20);
        foreach (var rows in new[] { NativeSingle(), NativeParallel() })
            ComparisonVerifier.Compare(expected, Normalize(rows), Operation, pair.ErrorBudget);
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public object Snapshot() => _owned.Snapshot();
    [Benchmark(Baseline = true)] public object NativeSingle() => Calculate(_quotes);
    private object Calculate(IEnumerable<Quote> input) => _trend
        ? (object)(List<PivotsResult>)input.GetPivots(2, 2, 20, EndType.HighLow)
        : (List<PivotPointsResult>)input.GetPivotPoints(PeriodSize.OneHour, PivotPointType.Standard);
    [Benchmark] public object NativeParallel()
    {
        Array output = _trend ? new PivotsResult[Count] : new PivotPointsResult[Count];
        if (_ends.Length == 0) return output;
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int start = worker == 0 ? 0 : _ends[worker - 1], end = _ends[worker];
            var rows = Calculate(_inputs[worker]);
            if (_trend) ((List<PivotsResult>)rows).CopyTo(start, (PivotsResult[])output, start, end - start);
            else ((List<PivotPointsResult>)rows).CopyTo(start, (PivotPointsResult[])output, start, end - start);
        });
        return output;
    }
    internal ComparisonSeries Normalize(object source)
    {
        if (_trend)
        {
            var rows = ((IEnumerable<PivotsResult>)source).ToArray();
            VerifyDates(rows.Select(r => r.Date));
            return PivotTrendComparison.Series([rows.Select(r => (double?)r.HighPoint).ToArray(),
                rows.Select(r => (double?)r.LowPoint).ToArray(), rows.Select(r => (double?)r.HighLine).ToArray(),
                rows.Select(r => (double?)r.LowLine).ToArray(),
                rows.Select(r => r.HighTrend.HasValue ? (double?)(int)r.HighTrend.Value : null).ToArray(),
                rows.Select(r => r.LowTrend.HasValue ? (double?)(int)r.LowTrend.Value : null).ToArray()]);
        }
        var levels = ((IEnumerable<PivotPointsResult>)source).ToArray();
        VerifyDates(levels.Select(r => r.Date));
        return PivotLevelComparison.Series([levels.Select(r => (double?)r.PP).ToArray(),
            levels.Select(r => (double?)r.S1).ToArray(), levels.Select(r => (double?)r.S2).ToArray(),
            levels.Select(r => (double?)r.S3).ToArray(), levels.Select(r => (double?)r.S4).ToArray(),
            levels.Select(r => (double?)r.R1).ToArray(), levels.Select(r => (double?)r.R2).ToArray(),
            levels.Select(r => (double?)r.R3).ToArray(), levels.Select(r => (double?)r.R4).ToArray()]);
    }
    private void VerifyDates(IEnumerable<DateTime> dates)
    {
        if (!dates.SequenceEqual(_quotes.Select(q => q.Date))) throw new InvalidOperationException("Native pivot dates differ.");
    }
}
