using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolatilityGeometryBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TypicalPriceVolatility", "StandardDeviationVolatility", "ClosedFormDistanceVolatility")]
    public string Operation { get; set; } = "TypicalPriceVolatility";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "TypicalPriceVolatility" => new TypicalPriceVolatility(),
        "StandardDeviationVolatility" => new StandardDeviationVolatility(14, 252),
        "ClosedFormDistanceVolatility" => new ClosedFormDistanceVolatility(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
