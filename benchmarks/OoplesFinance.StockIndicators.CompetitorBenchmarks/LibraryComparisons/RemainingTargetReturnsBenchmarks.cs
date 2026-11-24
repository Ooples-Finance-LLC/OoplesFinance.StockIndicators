using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTargetReturnsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TreynorRatio", "UpsidePotentialRatio")]
    public string Operation { get; set; } = "TreynorRatio";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "TreynorRatio" => new TreynorRatio(),
"UpsidePotentialRatio" => new UpsidePotentialRatio(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
