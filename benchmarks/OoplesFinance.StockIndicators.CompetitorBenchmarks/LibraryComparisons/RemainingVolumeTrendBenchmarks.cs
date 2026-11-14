using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare generated NVI/PVI/VROC/PVT against their own legacy route, preserving
// each staged averaging operation. No native startup convention is substituted.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVolumeTrendBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Nvi", "Pvi", "Vroc", "Pvt")]
    public string Operation { get; set; } = "Nvi";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Nvi" => new Nvi(20),
        "Pvi" => new Pvi(20),
        "Vroc" => new Vroc(20),
        "Pvt" => new Pvt(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
