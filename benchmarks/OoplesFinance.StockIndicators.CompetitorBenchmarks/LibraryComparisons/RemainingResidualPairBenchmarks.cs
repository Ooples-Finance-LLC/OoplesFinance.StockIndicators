using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingResidualPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("StandardErrorOfTheMean", "ErgodicMeanDeviationIndicator")]
    public string Operation { get; set; } = "StandardErrorOfTheMean";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "StandardErrorOfTheMean" => new StandardErrorCore(),
        "ErgodicMeanDeviationIndicator" => new ErgodicMeanDeviationIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
