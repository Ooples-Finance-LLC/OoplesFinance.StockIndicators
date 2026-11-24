using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRangeOhlcBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("RangeIdentifier", "RangeActionVerificationIndex", "RatioOchlAverager")]
    public string Operation { get; set; } = "RangeIdentifier";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "RangeIdentifier" => new RangeIdentifier(),
        "RangeActionVerificationIndex" => new RangeActionVerificationIndex(),
        "RatioOchlAverager" => new RatioOchlAverager(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
