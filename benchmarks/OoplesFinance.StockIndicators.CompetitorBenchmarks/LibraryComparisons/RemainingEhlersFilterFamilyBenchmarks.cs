using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingEhlersFilterFamilyBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersFilter", "EhlersAverageErrorFilter", "EhlersDistanceCoefficientFilter", "EhlersReflexIndicator", "EhlersTrendflexIndicator")]
    public string Operation { get; set; } = "EhlersFilter";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersFilter" => new EhlersFilter(),
        "EhlersAverageErrorFilter" => new EhlersAverageErrorFilter(),
        "EhlersDistanceCoefficientFilter" => new EhlersDistanceCoefficientFilter(),
        "EhlersReflexIndicator" => new EhlersReflexIndicator(),
        "EhlersTrendflexIndicator" => new EhlersTrendflexIndicator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
