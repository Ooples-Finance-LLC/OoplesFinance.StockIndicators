using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMarketMassBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MarketDirectionIndicator", "MassThrust", "MassThrustIndicator", "MassThrustOscillator")]
    public string Operation { get; set; } = "MarketDirectionIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "MarketDirectionIndicator" => new MarketDirectionIndicator(),
"MassThrust" => new MassThrust(),
"MassThrustIndicator" => new MassThrustIndicator(),
"MassThrustOscillator" => new MassThrustOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
