using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedGpuAroonBenchmarks
{
    [Params("TaLib.Functions.Aroon", "TaLib.Functions.AroonOsc", "Skender.GetAroon", "Trady.Indicator.Aroon", "Trady.Indicator.AroonOscillator")]
    public string Operation { get; set; } = "";
    [Params(10_000, 100_000)] public int Count { get; set; }
    private SharedAroonBenchmarks _cpu = null!;

    [GlobalSetup]
    public void Setup()
    {
        _cpu = new SharedAroonBenchmarks { Operation = Operation, Count = Count };
        _cpu.Setup();
        var indicator = AroonComparison.Create(Operation, 20);
        using var expected = _cpu.Build(indicator, IndicatorHistoryMode.Full);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var gpu = _cpu.Build(indicator, history, gpu: true);
            for (int slot = 0; slot < 6; slot++)
                AsinFeasibilityBenchmarks.RequireSame(expected[indicator.Outputs[slot]].ToArray(), gpu[indicator.Outputs[slot]].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => _cpu.Cleanup();
    [Benchmark] public IIndicatorRun BuilderFull() => _cpu.BuilderFull();
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => _cpu.BuilderLatestOnly();
    [Benchmark] public IIndicatorRun GpuFull() => _cpu.Build(AroonComparison.Create(Operation, 20), IndicatorHistoryMode.Full, gpu: true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => _cpu.Build(AroonComparison.Create(Operation, 20), IndicatorHistoryMode.LatestOnly, gpu: true);
    [Benchmark(Baseline = true)] public SharedAroonBenchmarks.NativeResult NativeSingle() => _cpu.NativeSingle();
    [Benchmark] public SharedAroonBenchmarks.NativeResult NativeParallel() => _cpu.NativeParallel();
}
