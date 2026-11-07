using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingOhlcVolatilityBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ParkinsonVolatility", "RogersSatchellVolatility", "YangZhangVolatility")]
    public string Operation { get; set; } = "ParkinsonVolatility";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "ParkinsonVolatility" => new ParkinsonVolatility(),
        "RogersSatchellVolatility" => new RogersSatchellVolatility(),
        "YangZhangVolatility" => new YangZhangVolatility(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
