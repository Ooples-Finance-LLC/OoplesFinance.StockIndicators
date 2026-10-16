using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingHeadleyRmseBandBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("PriceHeadleyAccelerationBands", "RootMovingAverageSquaredErrorBands")]
    public string Operation { get; set; } = "PriceHeadleyAccelerationBands";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "PriceHeadleyAccelerationBands" => new PriceHeadleyAccelerationBands(),
        "RootMovingAverageSquaredErrorBands" => new RootMovingAverageSquaredErrorBands(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
