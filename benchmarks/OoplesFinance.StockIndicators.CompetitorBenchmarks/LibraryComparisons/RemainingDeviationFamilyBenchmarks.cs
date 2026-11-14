using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDeviationFamilyBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersDeviationScaledMovingAverage", "EhlersFisherizedDeviationScaledOscillator", "EhlersDeviationScaledSuperSmoother")]
    public string Operation { get; set; } = "EhlersDeviationScaledMovingAverage";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersDeviationScaledMovingAverage" => new EhlersDeviationScaledMovingAverage(),
        "EhlersFisherizedDeviationScaledOscillator" => new EhlersFisherizedDeviationScaledOscillator(),
        "EhlersDeviationScaledSuperSmoother" => new EhlersDeviationScaledSuperSmoother(12, 2),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
