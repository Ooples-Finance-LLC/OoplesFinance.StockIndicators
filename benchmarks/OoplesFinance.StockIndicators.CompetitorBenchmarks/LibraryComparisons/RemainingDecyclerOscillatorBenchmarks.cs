using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDecyclerOscillatorBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersHighPassFilterV2", "EhlersDecyclerOscillatorV1", "EhlersDecyclerOscillatorV2")]
    public string Operation { get; set; } = "EhlersHighPassFilterV2";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersHighPassFilterV2" => new EhlersHighPassFilterV2(20),
        "EhlersDecyclerOscillatorV1" => new EhlersDecyclerOscillatorV1(20),
        "EhlersDecyclerOscillatorV2" => new EhlersDecyclerOscillatorV2(10, 20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
