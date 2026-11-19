using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingReverseSineBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersReverseEmaIndicatorV1", "EhlersReverseEmaIndicatorV2", "EhlersEvenBetterSineWaveIndicator")]
    public string Operation { get; set; } = "EhlersReverseEmaIndicatorV1";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersReverseEmaIndicatorV1" => new EhlersReverseEmaIndicatorV1(),
        "EhlersReverseEmaIndicatorV2" => new EhlersReverseEmaIndicatorV2(),
        "EhlersEvenBetterSineWaveIndicator" => new EhlersEvenBetterSineWaveIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
