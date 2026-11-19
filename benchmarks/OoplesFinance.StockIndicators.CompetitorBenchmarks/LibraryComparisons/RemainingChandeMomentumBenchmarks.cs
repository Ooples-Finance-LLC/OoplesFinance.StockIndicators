using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingChandeMomentumBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ChandeMomentumOscillatorAbsolute", "ChandeMomentumOscillatorAbsoluteAverage", "ChandeMomentumOscillatorAverage", "ChandeMomentumOscillatorAverageDisparityIndex", "ChandeMomentumOscillatorFilter")]
    public string Operation { get; set; } = "ChandeMomentumOscillatorAbsolute";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ChandeMomentumOscillatorAbsolute" => new ChandeMomentumOscillatorAbsolute(14),
        "ChandeMomentumOscillatorAbsoluteAverage" => new ChandeMomentumOscillatorAbsoluteAverage(14),
        "ChandeMomentumOscillatorAverage" => new ChandeMomentumOscillatorAverage(14),
        "ChandeMomentumOscillatorAverageDisparityIndex" => new ChandeMomentumOscillatorAverageDisparityIndex(14),
        "ChandeMomentumOscillatorFilter" => new ChandeMomentumOscillatorFilter(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
