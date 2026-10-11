using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;
using TI = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTrueRangeBenchmarks
{
    [Params("TaLib.Functions.TRange", "Skender.GetTr", "Trady.Indicator.TrueRange")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000, 100_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _high = null!, _low = null!, _close = null!;
    private Quote[] _quotes = null!;
    private (decimal High, decimal Low, decimal Close)[] _tuples = null!;
    private Quote[][] _quoteChunks = null!;
    private (decimal High, decimal Low, decimal Close)[][] _tupleChunks = null!;
    private int[] _skip = null!;
    private int _priorDop;
    private bool Skender => Operation.StartsWith("Skender.", StringComparison.Ordinal);
    private bool Trady => Operation.StartsWith("Trady.", StringComparison.Ordinal);

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism; CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _close = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
        _high = _close.Select(v => v + 1).ToArray(); _low = _close.Select(v => v - 1).ToArray();
        _bars = _close.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, _high[i], _low[i], v, 1)).ToArray();
        _quotes = _bars.Select(b => new Quote { Date = b.Time, Open = (decimal)b.Open, High = (decimal)b.High,
            Low = (decimal)b.Low, Close = (decimal)b.Close, Volume = 1 }).ToArray();
        _tuples = _bars.Select(b => ((decimal)b.High, (decimal)b.Low, (decimal)b.Close)).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _quoteChunks = new Quote[chunks][]; _tupleChunks = new (decimal, decimal, decimal)[chunks][]; _skip = new int[chunks];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks, seed = Math.Max(0, start - 1);
            _skip[chunk] = start - seed; _quoteChunks[chunk] = _quotes[seed..end]; _tupleChunks[chunk] = _tuples[seed..end];
        }
        var expected = new double[Count];
        for (int i = 0; i < Count; i++)
        {
            decimal high = (decimal)_high[i], low = (decimal)_low[i];
            expected[i] = (double)(i == 0 ? high - low : Math.Max(high - low,
                Math.Max(Math.Abs(high - (decimal)_close[i - 1]), Math.Abs(low - (decimal)_close[i - 1]))));
        }
        CheckNative(NativeSingle(), expected); CheckNative(NativeParallel(), expected);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool gpu in new[] { false, true })
        {
            var indicator = new TrueRange(1); using var run = Build(indicator, history, gpu);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
    }
    public readonly record struct NativeResult(double[]? Values = null, IEnumerable<TrResult>? Quotes = null, IReadOnlyList<decimal?>? Tuples = null);
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new TrueRange(1), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new TrueRange(1), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun GpuFull() => Build(new TrueRange(1), IndicatorHistoryMode.Full, true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => Build(new TrueRange(1), IndicatorHistoryMode.LatestOnly, true);
    [Benchmark(Baseline = true)] public NativeResult NativeSingle()
    {
        if (Skender) return new(Quotes: _quotes.GetTr());
        if (Trady) return new(Tuples: new TI.TrueRangeByTuple(_tuples).Compute());
        var result = new double[Count - 1]; NativeTa(1, Count, result); return new(Values: result);
    }
    [Benchmark] public NativeResult NativeParallel()
    {
        var result = Skender ? new NativeResult(Quotes: new TrResult[Count])
            : Trady ? new NativeResult(Tuples: new decimal?[Count]) : new NativeResult(Values: new double[Count - 1]);
        int chunks = _skip.Length;
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks;
            if (Skender)
            {
                int i = -_skip[chunk]; var target = (TrResult[])result.Quotes!;
                foreach (var row in _quoteChunks[chunk].GetTr()) { if (i >= 0) target[start + i] = row; i++; }
            }
            else if (Trady)
            {
                var rows = new TI.TrueRangeByTuple(_tupleChunks[chunk]).Compute(startIndex: _skip[chunk]);
                var target = (decimal?[])result.Tuples!;
                for (int i = 0; i < rows.Count; i++) target[start + i] = rows[i];
            }
            else NativeTa(Math.Max(1, start), end, result.Values!);
        });
        return result;
    }
    private void NativeTa(int start, int end, double[] output)
    {
        var status = Functions.TRange<double>(_high, _low, _close, new System.Range(start, end - 1), output.AsSpan(start - 1, end - start), out var range);
        if (status != TALib.Core.RetCode.Success || range.Start.Value != start || range.End.Value != end)
            throw new InvalidOperationException("Unexpected native true-range extent.");
    }
    private static void CheckNative(NativeResult result, double[] expected)
    {
        if (result.Values is not null) { AsinFeasibilityBenchmarks.RequireSame(expected.AsSpan(1).ToArray(), result.Values); return; }
        var values = result.Quotes is not null ? result.Quotes.Select(q => q.Tr).ToArray() : result.Tuples!.Select(v => (double?)v).ToArray();
        if (values.Length != expected.Length || values[0].HasValue || values.Skip(1).Any(v => !v.HasValue))
            throw new InvalidOperationException("Unexpected native true-range presence.");
        AsinFeasibilityBenchmarks.RequireSame(expected.AsSpan(1).ToArray(), values.Skip(1).Select(v => v!.Value).ToArray());
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu)
        {
            run.Dispose(); throw new InvalidOperationException("TrueRange did not dispatch GPU work.");
        }
        return run;
    }
}
