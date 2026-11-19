using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSimpleAndSigmaBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SimpleCycle", "SimplePriceZone", "SigmaSpikes")]
    public string Operation { get; set; } = "SimpleCycle";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SimpleCycle" => new SimpleCycle(),
"SimplePriceZone" => new SimplePriceZone(),
"SigmaSpikes" => new SigmaSpikes(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
