using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedConnorsBenchmarks
{
    [Params("Skender.GetConnorsRsi")] public string Operation { get; set; } = "Skender.GetConnorsRsi";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private (DateTime, double)[] _tuples = null!;
    private Bar[] _bars = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        if (Operation != "Skender.GetConnorsRsi") throw new ArgumentException("Unknown Connors operation.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _tuples = prices.Select((p, i) => (DateTime.UnixEpoch.AddMinutes(i), p)).ToArray();
        _bars = prices.Select((p, i) => new Bar(_tuples[i].Item1, p, p + 1, p - 1, p, 1)).ToArray();
        var nativeExpected = ConnorsComparison.Reference(prices, 3, 2, 100, true);
        var expected = ConnorsComparison.Reference(prices, 3, 2, 100, false);
        var serial = NativeSingle().ToArray();
        var parallel = NativeParallel();
        if (serial.Length != Count || parallel.Length != Count) throw new InvalidOperationException("Wrong native result count.");
        for (int i = 0; i < Count; i++)
        {
            if (serial[i].Date != _tuples[i].Item1 || parallel[i].Date != _tuples[i].Item1)
                throw new InvalidOperationException("Native dates changed.");
            for (int slot = 0; slot < 4; slot++)
            {
                var actual = Value(serial[i], slot);
                RequireEqual(nativeExpected[slot][i], actual);
                RequireEqual(actual, Value(parallel[i], slot));
                if (actual.HasValue != expected[slot][i].HasValue
                    || actual.HasValue && Math.Abs(actual.Value - expected[slot][i]!.Value) > 1e-9)
                    throw new InvalidOperationException("Native and owned Connors disagree on benchmark inputs.");
            }
        }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new ConnorsStrength();
            using var run = Build(indicator, history);
            for (int slot = 0; slot < 4; slot++)
            {
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(), run[indicator.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0).ToArray(), run[indicator.Outputs[slot + 4]].ToArray());
            }
        }
    }

    private static double? Value(ConnorsRsiResult row, int slot) => slot switch
        { 0 => row.Rsi, 1 => row.RsiStreak, 2 => row.PercentRank, _ => row.ConnorsRsi };
    private static void RequireEqual(double? expected, double? actual)
    {
        if (expected.HasValue != actual.HasValue || expected.HasValue
            && BitConverter.DoubleToInt64Bits(expected.Value) != BitConverter.DoubleToInt64Bits(actual!.Value))
            throw new InvalidOperationException("Connors output or presence mismatch.");
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new ConnorsStrength(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new ConnorsStrength(), IndicatorHistoryMode.LatestOnly);
    // The published tuple API computes eagerly and returns its List directly.
    // Retain that result with all nullable components; no measured normalization.
    [Benchmark(Baseline = true)] public List<ConnorsRsiResult> NativeSingle() => (List<ConnorsRsiResult>)_tuples.GetConnorsRsi();
    [Benchmark]
    public ConnorsRsiResult[] NativeParallel()
    {
        var result = new ConnorsRsiResult[Count];
        int chunks = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks;
            // Include all prefix calculation and temporary native results in
            // the timed arm; RSI recurrence cannot start afresh at each chunk.
            var prefix = _tuples.Take(end).GetConnorsRsi() as IReadOnlyList<ConnorsRsiResult>
                ?? throw new InvalidOperationException("Native Connors result is no longer eagerly materialized.");
            for (int i = start; i < end; i++) result[i] = prefix[i];
        });
        return result;
    }
    private IIndicatorRun Build(ConnorsStrength indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
