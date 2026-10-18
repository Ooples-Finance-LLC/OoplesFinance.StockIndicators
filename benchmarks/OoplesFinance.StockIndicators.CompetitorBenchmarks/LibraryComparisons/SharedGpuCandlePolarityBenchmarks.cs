using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Includes staging, upload, dispatch, readback and validation. Setup warms compilation
// and the bounded workspace; native methods return Trady's unnormalized bool lists.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedGpuCandlePolarityBenchmarks
{
    [Params("Bullish", "Bearish")] public string Operation { get; set; } = "";
    [Params(10_000, 100_000)] public int Count { get; set; }
    private SharedBasicCandleBenchmarks _work = null!;
    [GlobalSetup]
    public void Setup()
    {
        _work = new SharedBasicCandleBenchmarks { Operation = Operation, Count = Count };
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
    [Benchmark(Baseline = true)] public IReadOnlyList<bool> NativeSingle() => _work.NativeSingle();
    [Benchmark] public IReadOnlyList<bool>[] NativeParallel() => _work.NativeParallel();
}
