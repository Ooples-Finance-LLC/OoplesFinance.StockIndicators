using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Device timings include staging, both transfers, dispatch and output validation.
// Kernel compilation and the bounded workspace are warmed by setup.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedGpuEngulfingBenchmarks
{
    [Params(10_000, 100_000)] public int Count { get; set; }
    private SharedEngulfingBenchmarks _work = null!;

    [GlobalSetup]
    public void Setup()
    {
        _work = new SharedEngulfingBenchmarks { Count = Count };
        _work.Setup();
        var indicator = new EngulfingPattern();
        using var cpu = _work.Build(indicator, IndicatorHistoryMode.LatestOnly);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var gpu = _work.Build(indicator, history, backend: IndicatorExecutionBackend.Gpu);
            AsinFeasibilityBenchmarks.RequireSame(cpu[indicator].ToArray(), gpu[indicator].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => _work?.Cleanup();
    [Benchmark] public IIndicatorRun CpuFull() => _work.Build(new EngulfingPattern(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun CpuLatestOnly() => _work.Build(new EngulfingPattern(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun GpuFull() => _work.Build(new EngulfingPattern(), IndicatorHistoryMode.Full, backend: IndicatorExecutionBackend.Gpu);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => _work.Build(new EngulfingPattern(), IndicatorHistoryMode.LatestOnly, backend: IndicatorExecutionBackend.Gpu);
    [Benchmark(Baseline = true)] public int[] NativeSingle() => _work.NativeSingle();
    [Benchmark] public int[] NativeParallel() => _work.NativeParallel();
}
