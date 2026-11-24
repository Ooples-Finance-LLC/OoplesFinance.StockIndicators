using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPoleFilterBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Ehlers2PoleButterworthFilterV1", "Ehlers2PoleButterworthFilterV2", "Ehlers2PoleSuperSmootherFilterV1", "Ehlers2PoleSuperSmootherFilterV2", "Ehlers3PoleButterworthFilterV1", "Ehlers3PoleButterworthFilterV2", "Ehlers3PoleSuperSmootherFilter")]
    public string Operation { get; set; } = "Ehlers2PoleButterworthFilterV1";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Ehlers2PoleButterworthFilterV1" => new Ehlers2PoleButterworthFilterV1(20),
        "Ehlers2PoleButterworthFilterV2" => new Ehlers2PoleButterworthFilterV2(20),
        "Ehlers2PoleSuperSmootherFilterV1" => new Ehlers2PoleSuperSmootherFilterV1(20),
        "Ehlers2PoleSuperSmootherFilterV2" => new Ehlers2PoleSuperSmootherFilterV2(20),
        "Ehlers3PoleButterworthFilterV1" => new Ehlers3PoleButterworthFilterV1(20),
        "Ehlers3PoleButterworthFilterV2" => new Ehlers3PoleButterworthFilterV2(20),
        "Ehlers3PoleSuperSmootherFilter" => new Ehlers3PoleSuperSmootherFilter(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
