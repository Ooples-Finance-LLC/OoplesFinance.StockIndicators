using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRelativeVolatilityBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("RelativeVolatilityIndex", "RviVolatility", "RelativeVolatilityIndexV2", "RelativeVolatilityIndexHigh", "RelativeVolatilityIndexLow")]
    public string Operation { get; set; } = "RelativeVolatilityIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "RelativeVolatilityIndex" => new RelativeVolatilityIndex(),
        "RviVolatility" => new RviVolatility(),
        "RelativeVolatilityIndexV2" => new RelativeVolatilityIndexV2(),
        "RelativeVolatilityIndexHigh" => new RelativeVolatilityIndexHigh(),
        "RelativeVolatilityIndexLow" => new RelativeVolatilityIndexLow(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
