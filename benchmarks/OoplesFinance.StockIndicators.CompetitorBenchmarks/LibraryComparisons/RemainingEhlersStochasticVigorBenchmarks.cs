using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingEhlersStochasticVigorBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersStochasticCenterOfGravityOscillator", "EhlersStochastic", "EhlersStochasticCyberCycle", "EhlersRelativeVigorIndex")]
    public string Operation { get; set; } = "EhlersStochasticCenterOfGravityOscillator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersStochasticCenterOfGravityOscillator" => new EhlersStochasticCenterOfGravityOscillator(),
        "EhlersStochastic" => new EhlersStochastic(),
        "EhlersStochasticCyberCycle" => new EhlersStochasticCyberCycle(),
        "EhlersRelativeVigorIndex" => new EhlersRelativeVigorIndex(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
