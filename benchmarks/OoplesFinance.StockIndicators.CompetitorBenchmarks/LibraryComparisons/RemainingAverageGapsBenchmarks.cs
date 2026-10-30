using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAverageGapsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("PpoMa", "QmaSmaDifference", "QuadraticMovingAverage")]
    public string Operation { get; set; } = "PpoMa";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "PpoMa" => new PpoMa(),
"QmaSmaDifference" => new QmaSmaDifference(),
"QuadraticMovingAverage" => new QuadraticMovingAverage(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
