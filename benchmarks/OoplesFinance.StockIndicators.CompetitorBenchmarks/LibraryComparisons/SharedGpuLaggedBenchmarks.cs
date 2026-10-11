using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedGpuLaggedBenchmarks
{
    [Params("Mom", "RocR")] public string Operation { get; set; } = "";
    [Params(10_000, 100_000)] public int Count { get; set; }
    private SharedLaggedChangeBenchmarks _work = null!;
    [GlobalSetup]
    public void Setup()
    {
        _work = new SharedLaggedChangeBenchmarks { Operation = Operation, Count = Count };
        _work.Setup();
        var indicator = _work.Create();
        using var cpu = _work.Build(indicator, IndicatorHistoryMode.LatestOnly);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var gpu = _work.Build(indicator, history, backend: IndicatorExecutionBackend.Gpu);
            AsinFeasibilityBenchmarks.RequireSame(cpu[indicator].ToArray(), gpu[indicator].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => _work?.Cleanup();
    [Benchmark] public IIndicatorRun CpuFull() => _work.Build(_work.Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun CpuLatestOnly() => _work.Build(_work.Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun GpuFull() => _work.Build(_work.Create(), IndicatorHistoryMode.Full, backend: IndicatorExecutionBackend.Gpu);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => _work.Build(_work.Create(), IndicatorHistoryMode.LatestOnly, backend: IndicatorExecutionBackend.Gpu);
    [Benchmark(Baseline = true)] public double[] NativeSingle() => _work.NativeSingle();
    [Benchmark] public double[] NativeParallel() => _work.NativeParallel();
}
