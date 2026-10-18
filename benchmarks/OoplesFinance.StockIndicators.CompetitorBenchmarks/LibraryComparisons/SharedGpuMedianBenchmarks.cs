using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedGpuMedianBenchmarks
{
    [Params("Trady.Indicator.Median", "QuanTAlib.Median", "Trady.Indicator.Percentile", "QuanTAlib.Percentile")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000, 100_000)] public int Count { get; set; }
    private SharedRollingPercentileBenchmarks _cpu = null!;
    [GlobalSetup]
    public void Setup()
    {
        _cpu = new SharedRollingPercentileBenchmarks { Operation = Operation, Count = Count }; _cpu.Setup();
        var indicator = _cpu.Create();
        using var expected = _cpu.Build(indicator, IndicatorHistoryMode.Full);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var gpu = _cpu.Build(indicator, history, gpu: true);
            AsinFeasibilityBenchmarks.RequireSame(expected[indicator].ToArray(), gpu[indicator].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => _cpu.Cleanup();
    [Benchmark] public IIndicatorRun BuilderFull() => _cpu.BuilderFull();
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => _cpu.BuilderLatestOnly();
    [Benchmark] public IIndicatorRun GpuFull() => _cpu.Build(_cpu.Create(), IndicatorHistoryMode.Full, gpu: true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => _cpu.Build(_cpu.Create(), IndicatorHistoryMode.LatestOnly, gpu: true);
    [Benchmark(Baseline = true)] public SharedRollingPercentileBenchmarks.NativeResult NativeSingle() => _cpu.NativeSingle();
    [Benchmark] public SharedRollingPercentileBenchmarks.NativeResult NativeParallel() => _cpu.NativeParallel();
}
