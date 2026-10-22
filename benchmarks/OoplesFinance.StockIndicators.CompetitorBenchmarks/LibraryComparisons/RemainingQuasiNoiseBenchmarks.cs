using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingQuasiNoiseBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("QuasiWhiteNoise")]
    public string Operation { get; set; } = "QuasiWhiteNoise";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "QuasiWhiteNoise" => new QuasiWhiteNoise(20, 500, 40),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
