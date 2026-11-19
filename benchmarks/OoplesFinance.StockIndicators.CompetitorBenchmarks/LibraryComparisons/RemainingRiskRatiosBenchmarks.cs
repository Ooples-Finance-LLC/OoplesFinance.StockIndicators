using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRiskRatiosBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("InformationRatio", "SharpeRatio", "SortinoRatio")]
    public string Operation { get; set; } = "InformationRatio";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "InformationRatio" => new InformationRatio(14),
        "SharpeRatio" => new SharpeRatio(14),
        "SortinoRatio" => new SortinoRatio(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
