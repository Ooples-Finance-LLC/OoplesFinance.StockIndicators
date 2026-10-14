using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedDeviationRatioBenchmarks
{
    private const int Period = 20, Length = 80;
    private const double Alpha = .2;
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _ticks = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _ticks = prices.Select((p, i) => new QuanTAlib.TValue(DateTime.UnixEpoch.AddMinutes(i), p, true, false)).ToArray();
        _bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p + 1, p - 1, p, 1)).ToArray();
        var expected = DeviationRatioComparison.GridReference(prices, Period, Length, Alpha);
        var nativeExpected = DeviationRatioComparison.NativeReference(prices, Period, Length, Alpha);
        var native = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(nativeExpected, native);
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        for (int i = 0; i < Count; i++)
            if (!double.IsFinite(native[i]) || !double.IsFinite(expected[i]) || Math.Abs(expected[i] - native[i]) > 1e-9)
                throw new InvalidOperationException($"Native and owned VIDYA disagree at {i}.");
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator.Average].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(Enumerable.Repeat(1d, Count).ToArray(), run[indicator.IsDefined].ToArray());
        }
    }
    private static DeviationRatioAdaptiveAverage Create() => new(Period, Length, Alpha);
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var result = new double[Count]; Calculate(0, Count, result); return result;
    }
    [Benchmark] public double[] NativeParallel()
    {
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        var result = new double[Count];
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            Calculate(Count * worker / workers, Count * (worker + 1) / workers, result));
        return result;
    }
    private void Calculate(int start, int end, double[] result)
    {
        // Complete prefix replay preserves both rolling windows and recurrence.
        // Native state allocations and discarded prefix work remain timed.
        var state = new QuanTAlib.Vidya(Period, Length, Alpha);
        for (int i = 0; i < start; i++) state.Calc(_ticks[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_ticks[i]).Value;
    }
    private IIndicatorRun Build(DeviationRatioAdaptiveAverage indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
