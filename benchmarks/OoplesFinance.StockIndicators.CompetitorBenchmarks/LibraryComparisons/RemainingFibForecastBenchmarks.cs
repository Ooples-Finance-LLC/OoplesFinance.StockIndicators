using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingFibForecastBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("FibonacciRetrace", "ForecastOscillator")]
    public string Operation { get; set; } = "FibonacciRetrace";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "FibonacciRetrace" => new FibonacciRetrace(),
        "ForecastOscillator" => new ForecastOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
