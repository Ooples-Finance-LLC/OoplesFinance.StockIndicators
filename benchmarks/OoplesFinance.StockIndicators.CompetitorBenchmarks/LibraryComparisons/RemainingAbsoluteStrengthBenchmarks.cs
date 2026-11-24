using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAbsoluteStrengthBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AbsoluteStrengthIndex", "AbsoluteStrengthMTFIndicator")]
    public string Operation { get; set; } = "AbsoluteStrengthIndex";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AbsoluteStrengthIndex" => new AbsoluteStrengthIndex(10),
        "AbsoluteStrengthMTFIndicator" => new AbsoluteStrengthMTFIndicator(50, 25),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
