using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingFractalChaosBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("FractalChaosBands", "FractalChaosOscillator")]
    public string Operation { get; set; } = "FractalChaosBands";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "FractalChaosBands" => new FractalChaosBands(),
        "FractalChaosOscillator" => new FractalChaosOscillator(5),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
