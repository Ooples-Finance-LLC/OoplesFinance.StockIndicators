using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingEhlersPhaseGravityBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersAllPassPhaseShifter", "EhlersCenterOfGravityOscillator", "EhlersCenterofGravityOscillator", "EhlersChebyshevLowPassFilter")]
    public string Operation { get; set; } = "EhlersAllPassPhaseShifter";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersAllPassPhaseShifter" => new EhlersAllPassPhaseShifter(),
        "EhlersCenterOfGravityOscillator" => new EhlersCenterOfGravityOscillator(),
        "EhlersCenterofGravityOscillator" => new EhlersCenterofGravityOscillator(),
        "EhlersChebyshevLowPassFilter" => new EhlersChebyshevLowPassFilter(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
