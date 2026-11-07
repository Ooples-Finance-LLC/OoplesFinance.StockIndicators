using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingInheritedCoreBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("BalanceOfPower", "Ema", "HighestHigh", "LowestLow", "TrueRange", "Wma", "WilliamsR")]
    public string Operation { get; set; } = "BalanceOfPower";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "BalanceOfPower" => new BalanceOfPower(14),
        "Ema" => new Ema(14),
        "HighestHigh" => new HighestHigh(14),
        "LowestLow" => new LowestLow(14),
        "TrueRange" => new TrueRange(1),
        "Wma" => new Wma(14),
        "WilliamsR" => new WilliamsR(14),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
