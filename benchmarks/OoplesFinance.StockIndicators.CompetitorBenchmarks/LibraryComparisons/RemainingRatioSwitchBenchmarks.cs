using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRatioSwitchBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("VolatilityRatio", "VolatilitySwitchIndicator")]
    public string Operation { get; set; } = "VolatilityRatio";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "VolatilityRatio" => new VolatilityRatio(),
        "VolatilitySwitchIndicator" => new VolatilitySwitchIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
