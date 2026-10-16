using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedGpuPathRatioBenchmarks
{
    [Params("Skender.GetCmo", "Trady.Indicator.EfficiencyRatio")] public string Operation { get; set; } = "Skender.GetCmo";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private SharedPathRatioBenchmarks _cpu = null!;
    [GlobalSetup]
    public void Setup()
    {
        _cpu = new SharedPathRatioBenchmarks { Operation = Operation, Count = Count };
        _cpu.Setup();
        var indicator = _cpu.Create();
        using var expected = _cpu.Build(indicator, IndicatorHistoryMode.Full);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var gpu = _cpu.Build(indicator, history, gpu: true);
            foreach (var output in indicator.Outputs)
            {
                var actual = gpu[output].ToArray();
                if (actual.Length != Count) throw new InvalidOperationException("GPU path-ratio column length differs.");
                AsinFeasibilityBenchmarks.RequireSame(expected[output].ToArray(), actual);
            }
        }
    }
    [GlobalCleanup] public void Cleanup() => _cpu.Cleanup();
    [Benchmark] public IIndicatorRun BuilderFull() => _cpu.BuilderFull();
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => _cpu.BuilderLatestOnly();
    [Benchmark] public IIndicatorRun GpuFull() => _cpu.Build(_cpu.Create(), IndicatorHistoryMode.Full, gpu: true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => _cpu.Build(_cpu.Create(), IndicatorHistoryMode.LatestOnly, gpu: true);
    [Benchmark(Baseline = true)] public object NativeSingle() => _cpu.NativeSingle();
    [Benchmark] public object NativeParallel() => _cpu.NativeParallel();
}
