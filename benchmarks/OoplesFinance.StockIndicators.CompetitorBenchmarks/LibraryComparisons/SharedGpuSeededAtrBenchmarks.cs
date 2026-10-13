using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedGpuSeededAtrBenchmarks
{
    [Params("Skender.GetAtr", "TaLib.Functions.Atr", "TaLib.Functions.Natr", "Trady.Indicator.AverageTrueRange")]
    public string Operation { get; set; } = "Skender.GetAtr";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private SharedSeededAtrBenchmarks _cpu = null!;

    [GlobalSetup]
    public void Setup()
    {
        _cpu = new SharedSeededAtrBenchmarks { Operation = Operation, Count = Count, UseGpuGrid = true };
        try
        {
            _cpu.Setup();
            var indicator = _cpu.Create();
            using var expected = _cpu.Build(indicator, IndicatorHistoryMode.Full);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var gpu = _cpu.Build(indicator, history, gpu: true);
                foreach (var output in indicator.Outputs)
                {
                    var actual = gpu[output].ToArray();
                    var reference = expected[output].ToArray();
                    if (actual.Length != Count || reference.Length != Count)
                        throw new InvalidOperationException("GPU seeded ATR column length differs.");
                    for (int i = 0; i < Count; i++)
                        if (BitConverter.DoubleToInt64Bits(actual[i]) != BitConverter.DoubleToInt64Bits(reference[i]))
                            throw new InvalidOperationException($"GPU seeded ATR column differs at {i}.");
                }
            }
        }
        catch { Cleanup(); throw; }
    }

    [GlobalCleanup] public void Cleanup() => _cpu?.Cleanup();
    [Benchmark] public IIndicatorRun BuilderFull() => _cpu.BuilderFull();
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => _cpu.BuilderLatestOnly();
    [Benchmark] public IIndicatorRun GpuFull() => _cpu.Build(_cpu.Create(), IndicatorHistoryMode.Full, gpu: true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => _cpu.Build(_cpu.Create(), IndicatorHistoryMode.LatestOnly, gpu: true);
    [Benchmark(Baseline = true)] public SharedSeededAtrBenchmarks.NativeResult NativeSingle() => _cpu.NativeSingle();
    [Benchmark] public SharedSeededAtrBenchmarks.NativeResult NativeParallel() => _cpu.NativeParallel();
}
