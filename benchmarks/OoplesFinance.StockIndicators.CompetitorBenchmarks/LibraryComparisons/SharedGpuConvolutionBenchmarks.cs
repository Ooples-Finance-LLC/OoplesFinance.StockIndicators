using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedGpuConvolutionBenchmarks
{
    [Params("Convolution")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000, 100_000)] public int Count { get; set; }
    private SharedWeightedWindowBenchmarks _cpu = null!;
    [GlobalSetup]
    public void Setup()
    {
        _cpu = new SharedWeightedWindowBenchmarks { Operation = Operation, Count = Count };
        _cpu.Setup();
        SharedWindowBenchmarkValidation.Gpu(_cpu.Create(), _cpu.Build);
    }
    [GlobalCleanup] public void Cleanup() => _cpu.Cleanup();
    [Benchmark] public IIndicatorRun BuilderFull() => _cpu.BuilderFull();
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => _cpu.BuilderLatestOnly();
    [Benchmark] public IIndicatorRun GpuFull() => _cpu.Build(_cpu.Create(), IndicatorHistoryMode.Full, gpu: true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => _cpu.Build(_cpu.Create(), IndicatorHistoryMode.LatestOnly, gpu: true);
    [Benchmark(Baseline = true)] public double[] NativeSingle() => _cpu.NativeSingle();
    [Benchmark] public double[] NativeParallel() => _cpu.NativeParallel();
}
