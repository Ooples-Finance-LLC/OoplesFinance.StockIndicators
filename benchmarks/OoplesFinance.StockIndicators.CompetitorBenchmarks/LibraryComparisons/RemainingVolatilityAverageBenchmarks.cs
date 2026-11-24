using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolatilityAverageBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("VolatilityMovingAverage")]
    public string Operation { get; set; } = "VolatilityMovingAverage";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "VolatilityMovingAverage" => new VolatilityMovingAverage(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
