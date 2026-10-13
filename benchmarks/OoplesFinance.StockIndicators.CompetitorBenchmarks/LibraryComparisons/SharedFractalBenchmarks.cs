using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedFractalBenchmarks
{
    private const int Period = 20;
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _input = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _bars = CompetitorData.FromCloses(prices).IndicatorBars;
        _input = _bars.Select(b => new QuanTAlib.TValue(b.Time, b.Close, true)).ToArray();
        var pair = RangeAdaptiveComparison.Pair(true);
        var data = CompetitorData.FromCloses(prices);
        var expected = pair.Reference!(data, Period);
        var serial = NativeSingle();
        Check(pair.CompetitorReference!(data, Period), serial);
        var parallel = NativeParallel();
        if (!serial.Select(BitConverter.DoubleToInt64Bits).SequenceEqual(parallel.Select(BitConverter.DoubleToInt64Bits)))
            throw new InvalidOperationException("Fractal native prefix replay changed rounding.");
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var root = new RangeFractalAverage(Period);
            using var run = Build(root, mode);
            Check(expected, run[root.Value].ToArray());
        }
    }

    private static void Check(ComparisonSeries expected, double[] values) => ComparisonVerifier.Compare(expected,
        VolumePriceComparison.Mask(values.Select(v => (double?)v).ToArray()), "shared fractal", OoplesFinance.StockIndicators.Validation.IndicatorErrorBudget.Exact);

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new RangeFractalAverage(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new RangeFractalAverage(Period), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var result = new double[Count]; Calculate(0, Count, result); return result;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var result = new double[Count];
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            Calculate(Count * worker / workers, Count * (worker + 1) / workers, result));
        return result;
    }
    private void Calculate(int start, int end, double[] result)
    {
        var state = new QuanTAlib.Frama(Period);
        // The rounded recursive state depends on the complete prefix. Each
        // native worker replays its prefix through the same public API; no
        // window-only seed or reassociation substitutes a different recurrence.
        int origin = 0;
        for (int i = origin; i < end; i++)
        {
            double value = state.Calc(_input[i]).Value;
            if (i >= start) result[i] = value;
        }
    }
    private IIndicatorRun Build(IIndicator root, IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(root).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
