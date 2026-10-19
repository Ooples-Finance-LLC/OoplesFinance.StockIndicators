using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingBreadthMidpointBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("McClellanOscillator", "MidpointOscillator")]
    public string Operation { get; set; } = "McClellanOscillator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "McClellanOscillator" => new McClellanOscillator(14),
        "MidpointOscillator" => new MidpointOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
