using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingNaturalDirectionalBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("NaturalDirectionalIndex", "NaturalStochasticIndicator")]
    public string Operation { get; set; } = "NaturalDirectionalIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "NaturalDirectionalIndex" => new NaturalDirectionalIndex(),
"NaturalStochasticIndicator" => new NaturalStochasticIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
