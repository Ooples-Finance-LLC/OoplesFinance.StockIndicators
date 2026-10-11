using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRollingSumBenchmarks
{
    private const int Period = 20;
    [Params(1_000, 10_000, 100_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _closes = null!;
    private int _previousDop;
    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        (_closes, _bars) = PackedWindowBenchmarkExecution.Input(Count);
        var startup = new double[Period - 1];
        PackedWindowBenchmarkExecution.Verify(NativeSingle(), NativeParallel(), startup,
            () => new RollingPriceSum(Period), Build);
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new RollingPriceSum(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new RollingPriceSum(Period), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun GpuFull() => Build(new RollingPriceSum(Period), IndicatorHistoryMode.Full, true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => Build(new RollingPriceSum(Period), IndicatorHistoryMode.LatestOnly, true);
    [Benchmark(Baseline = true)] public double[] NativeSingle() =>
        PackedWindowBenchmarkExecution.Single<TaSumOperation>(_closes, Period);
    [Benchmark] public double[] NativeParallel() =>
        PackedWindowBenchmarkExecution.Parallel<TaSumOperation>(_closes, Period);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode mode, bool gpu = false) =>
        PackedWindowBenchmarkExecution.Build(_bars, indicator, mode, gpu);
}
