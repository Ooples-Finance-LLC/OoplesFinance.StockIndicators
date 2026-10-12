using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedUlcerBenchmarks
{
    private const int Period = 14;
    [Params("Skender.GetUlcerIndex")] public string Operation { get; set; } = "Skender.GetUlcerIndex";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private (DateTime, double)[] _tuples = null!;
    private (DateTime, double)[][] _chunks = null!;
    private int[] _skip = null!;
    private Bar[] _bars = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        if (Operation != "Skender.GetUlcerIndex") throw new ArgumentException("Unknown Ulcer Index operation.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _tuples = prices.Select((p, i) => (DateTime.UnixEpoch.AddMinutes(i), p)).ToArray();
        _bars = prices.Select((p, i) => new Bar(_tuples[i].Item1, p, p + 1, p - 1, p, 1)).ToArray();
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _chunks = new (DateTime, double)[workers][];
        _skip = new int[workers];
        for (int worker = 0; worker < workers; worker++)
        {
            int start = Count * worker / workers, end = Count * (worker + 1) / workers;
            int seed = Math.Max(0, start - Period + 1);
            _chunks[worker] = _tuples[seed..end];
            _skip[worker] = start - seed;
        }
        var nativeExpected = UlcerComparison.Reference(prices, Period, true);
        var expected = UlcerComparison.Reference(prices, Period, false);
        var serial = NativeSingle();
        var parallel = NativeParallel();
        if (serial.Count != Count || parallel.Length != Count) throw new InvalidOperationException("Wrong native result count.");
        for (int i = 0; i < Count; i++)
        {
            if (serial[i].Date != _tuples[i].Item1 || parallel[i].Date != _tuples[i].Item1)
                throw new InvalidOperationException("Native dates changed.");
            RequireEqual(nativeExpected[i], serial[i].UI);
            RequireEqual(serial[i].UI, parallel[i].UI);
            if (serial[i].UI.HasValue != expected[i].HasValue
                || serial[i].UI.HasValue && Math.Abs(serial[i].UI!.Value - expected[i]!.Value) > 1e-9)
                throw new InvalidOperationException("Native and owned Ulcer Index disagree on benchmark inputs.");
        }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new WindowUlcerIndex(Period);
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v ?? 0).ToArray(), run[indicator.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v.HasValue ? 1d : 0).ToArray(), run[indicator.IsDefined].ToArray());
        }
    }

    private static void RequireEqual(double? expected, double? actual)
    {
        if (expected.HasValue != actual.HasValue || expected.HasValue
            && BitConverter.DoubleToInt64Bits(expected.Value) != BitConverter.DoubleToInt64Bits(actual!.Value))
            throw new InvalidOperationException("Ulcer Index output or presence mismatch.");
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new WindowUlcerIndex(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new WindowUlcerIndex(Period), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public List<UlcerIndexResult> NativeSingle() => (List<UlcerIndexResult>)_tuples.GetUlcerIndex(Period);
    [Benchmark]
    public UlcerIndexResult[] NativeParallel()
    {
        var output = new UlcerIndexResult[Count];
        // Independent windows need only P-1 input overlap. Native sorting,
        // indicator construction, temporary outputs and result assembly are timed.
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, worker =>
        {
            var local = (List<UlcerIndexResult>)_chunks[worker].GetUlcerIndex(Period);
            int start = Count * worker / _chunks.Length;
            for (int i = _skip[worker]; i < local.Count; i++) output[start + i - _skip[worker]] = local[i];
        });
        return output;
    }
    private IIndicatorRun Build(WindowUlcerIndex indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
