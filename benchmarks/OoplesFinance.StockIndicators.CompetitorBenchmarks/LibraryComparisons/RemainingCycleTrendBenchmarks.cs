using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCycleTrendBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersCyberCycle", "EhlersCyberCycleOscillator", "EhlersInstantaneousTrendlineV2", "EhlersGaussianFilter")]
    public string Operation { get; set; } = "EhlersCyberCycle";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersCyberCycle" => new EhlersCyberCycle(20),
        "EhlersCyberCycleOscillator" => new EhlersCyberCycleOscillator(.07),
        "EhlersInstantaneousTrendlineV2" => new EhlersInstantaneousTrendlineV2(.07),
        "EhlersGaussianFilter" => new EhlersGaussianFilter(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
