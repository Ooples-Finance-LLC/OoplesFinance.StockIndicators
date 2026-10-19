using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingOhlcOscillatorsBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("RelativeVigorIndex", "Repulse")]
    public string Operation { get; set; } = "RelativeVigorIndex";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "RelativeVigorIndex" => new RelativeVigorIndex(),
        "Repulse" => new Repulse(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
