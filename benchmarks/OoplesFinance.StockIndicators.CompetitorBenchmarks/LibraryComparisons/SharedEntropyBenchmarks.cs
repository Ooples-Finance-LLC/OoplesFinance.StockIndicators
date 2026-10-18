using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedEntropyBenchmarks
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
        var prices = Enumerable.Range(0, Count).Select(i => i % 137 < 41 ? 3 : (i * 17 % 23) / 4d).ToArray();
        _bars = CompetitorData.FromCloses(prices).IndicatorBars;
        _input = _bars.Select(b => new QuanTAlib.TValue(b.Time, b.Close, true)).ToArray();
        var expected = EntropyComparison.Reference(prices, Period);
        var serial = NativeSingle();
        Check(expected, serial);
        var parallel = NativeParallel();
        if (!serial.Select(BitConverter.DoubleToInt64Bits).SequenceEqual(parallel.Select(BitConverter.DoubleToInt64Bits)))
            throw new InvalidOperationException("Entropy native worker replay changed rounding.");
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var root = new NormalizedWindowEntropy(Period);
            using var run = Build(root, mode);
            Check(expected, run[root.Value].ToArray());
        }
    }

    private static void Check(ComparisonSeries expected, double[] values) => ComparisonVerifier.Compare(expected,
        VolumePriceComparison.Mask(values.Select(v => (double?)v).ToArray()), "shared entropy", EntropyComparison.Pair.ErrorBudget!);

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new NormalizedWindowEntropy(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new NormalizedWindowEntropy(Period), IndicatorHistoryMode.LatestOnly);
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
        var state = new QuanTAlib.Entropy(Period);
        // Native rebuilds its frequency groups from its buffer on every call.
        // Keep both the preceding window and its ring phase, so even physical
        // buffer enumeration preserves serial binary64 summation order.
        int origin = Math.Max(0, start - Period + 1);
        origin -= origin % Period;
        for (int i = origin; i < end; i++)
        {
            double value = state.Calc(_input[i]).Value;
            if (i >= start) result[i] = value;
        }
    }
    private IIndicatorRun Build(IIndicator root, IndicatorHistoryMode mode) => SharedBenchmarkSupport.BuildCpu(_bars, root, mode);
}
