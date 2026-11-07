using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRoofingBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersRoofingFilter", "EhlersRoofingFilterV1", "EhlersRoofingFilterV2", "EhlersRoofingFilterIndicator", "EhlersHpLpRoofingFilter", "EhlersZeroMeanRoofingFilter", "EhlersDetrendedLeadingIndicator")]
    public string Operation { get; set; } = "EhlersRoofingFilter";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersRoofingFilter" => new EhlersRoofingFilter(20, 10),
        "EhlersRoofingFilterV1" => new EhlersRoofingFilterV1(20, 10),
        "EhlersRoofingFilterV2" => new EhlersRoofingFilterV2(20, 10),
        "EhlersRoofingFilterIndicator" => new EhlersRoofingFilterIndicator(20, 10),
        "EhlersHpLpRoofingFilter" => new EhlersHpLpRoofingFilter(20, 10),
        "EhlersZeroMeanRoofingFilter" => new EhlersZeroMeanRoofingFilter(20, 10),
        "EhlersDetrendedLeadingIndicator" => new EhlersDetrendedLeadingIndicator(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
