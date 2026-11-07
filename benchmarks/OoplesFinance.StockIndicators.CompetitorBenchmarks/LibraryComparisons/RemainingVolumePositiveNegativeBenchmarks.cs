using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolumePositiveNegativeBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("VolumePositiveNegativeIndicator")]
    public string Operation { get; set; } = "VolumePositiveNegativeIndicator";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "VolumePositiveNegativeIndicator" => new VolumePositiveNegativeIndicator(30, 3),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
