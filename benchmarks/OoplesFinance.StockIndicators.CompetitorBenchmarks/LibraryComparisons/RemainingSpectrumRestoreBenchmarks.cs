using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSpectrumRestoreBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersSpectrumDerivedFilterBank", "EhlersRestoringPullIndicator")]
    public string Operation { get; set; } = "EhlersSpectrumDerivedFilterBank";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EhlersSpectrumDerivedFilterBank" => new EhlersSpectrumDerivedFilterBank(),
        "EhlersRestoringPullIndicator" => new EhlersRestoringPullIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
