using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingEhlersFilterBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersIirFilter", "EhlersInfiniteImpulseResponseFilter", "EhlersFirFilter", "EhlersFiniteImpulseResponseFilter", "EhlersOptimumEllipticFilter", "EhlersModifiedOptimumEllipticFilter")]
    public string Operation { get; set; } = "EhlersIirFilter";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersIirFilter" => new EhlersIirFilter(20),
        "EhlersInfiniteImpulseResponseFilter" => new EhlersInfiniteImpulseResponseFilter(20),
        "EhlersFirFilter" => new EhlersFirFilter(20),
        "EhlersFiniteImpulseResponseFilter" => new EhlersFiniteImpulseResponseFilter(20),
        "EhlersOptimumEllipticFilter" => new EhlersOptimumEllipticFilter(20),
        "EhlersModifiedOptimumEllipticFilter" => new EhlersModifiedOptimumEllipticFilter(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
