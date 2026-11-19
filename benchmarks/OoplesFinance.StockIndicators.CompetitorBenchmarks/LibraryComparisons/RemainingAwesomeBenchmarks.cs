using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAwesomeBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AwesomeOscillator", "AcceleratorOscillator")]
    public string Operation { get; set; } = "AwesomeOscillator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AwesomeOscillator" => new AwesomeOscillator(5),
        "AcceleratorOscillator" => new AcceleratorOscillator(5),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
