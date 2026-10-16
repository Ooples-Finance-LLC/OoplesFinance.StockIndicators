using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedGpuCmfBenchmarks
{
    [Params("Skender.GetCmf")] public string Operation { get; set; } = "Skender.GetCmf";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private SharedCmfBenchmarks _cpu = null!;

    [GlobalSetup]
    public void Setup()
    {
        _cpu = new SharedCmfBenchmarks { Operation = Operation, Count = Count };
        try
        {
            _cpu.Setup();
            var indicator = new MoneyFlowWithDetails(20);
            using var expected = _cpu.Build(indicator, IndicatorHistoryMode.Full);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var gpu = _cpu.Build(indicator, history, gpu: true);
                foreach (var output in indicator.Outputs)
                {
                    var actual = gpu[output].ToArray();
                    var reference = expected[output].ToArray();
                    if (actual.Length != Count || reference.Length != Count)
                        throw new InvalidOperationException("GPU CMF column length differs.");
                    for (int i = 0; i < Count; i++)
                        if (BitConverter.DoubleToInt64Bits(actual[i]) != BitConverter.DoubleToInt64Bits(reference[i]))
                            throw new InvalidOperationException($"GPU CMF column differs at {i}.");
                }
            }
        }
        catch { Cleanup(); throw; }
    }

    [GlobalCleanup] public void Cleanup() => _cpu?.Cleanup();
    [Benchmark] public IIndicatorRun BuilderFull() => _cpu.BuilderFull();
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => _cpu.BuilderLatestOnly();
    [Benchmark] public IIndicatorRun GpuFull() => _cpu.Build(new(20), IndicatorHistoryMode.Full, gpu: true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => _cpu.Build(new(20), IndicatorHistoryMode.LatestOnly, gpu: true);
    [Benchmark(Baseline = true)] public object NativeSingle() => _cpu.NativeSingle();
    [Benchmark] public object NativeParallel() => _cpu.NativeParallel();
}
