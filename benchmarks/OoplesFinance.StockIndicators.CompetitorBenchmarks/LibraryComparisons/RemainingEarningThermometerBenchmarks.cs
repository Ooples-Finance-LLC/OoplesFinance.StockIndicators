using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingEarningThermometerBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EarningSupportResistanceLevels", "ElderMarketThermometer")]
    public string Operation { get; set; } = "EarningSupportResistanceLevels";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "EarningSupportResistanceLevels" => new EarningSupportResistanceLevels(),
        "ElderMarketThermometer" => new ElderMarketThermometer(22),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
