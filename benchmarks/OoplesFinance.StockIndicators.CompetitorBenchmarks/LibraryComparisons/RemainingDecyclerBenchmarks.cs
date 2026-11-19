using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDecyclerBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersDecycler", "EhlersHighPassFilterV1", "EhlersLeadingIndicator", "EhlersSimpleDecycler")]
    public string Operation { get; set; } = "EhlersDecycler";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersDecycler" => new EhlersDecycler(20),
        "EhlersHighPassFilterV1" => new EhlersHighPassFilterV1(20),
        "EhlersLeadingIndicator" => new EhlersLeadingIndicator(20),
        "EhlersSimpleDecycler" => new EhlersSimpleDecycler(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
