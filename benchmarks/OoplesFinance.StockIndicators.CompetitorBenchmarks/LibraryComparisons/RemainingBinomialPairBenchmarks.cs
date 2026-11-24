using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingBinomialPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("PentupleExponentialMovingAverage", "QuadrupleExponentialMovingAverage")]
    public string Operation { get; set; } = "PentupleExponentialMovingAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "PentupleExponentialMovingAverage" => new PentupleExponentialMovingAverage(),
"QuadrupleExponentialMovingAverage" => new QuadrupleExponentialMovingAverage(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
