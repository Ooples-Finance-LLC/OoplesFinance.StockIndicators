using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare generated ZLEMA/TMA/HMA against their own legacy route, preserving
// each staged averaging operation. No native startup convention is substituted.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingComposedAverageBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Zlema", "Tma", "Hma")]
    public string Operation { get; set; } = "Zlema";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Zlema" => new Zlema(20),
        "Tma" => new Tma(20),
        "Hma" => new Hma(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
