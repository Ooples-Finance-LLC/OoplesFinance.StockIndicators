using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRegressionPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TrigonometricOscillator", "TimeSeriesForecast")]
    public string Operation { get; set; } = "TimeSeriesForecast";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "TrigonometricOscillator" => new TrigonometricOscillator(),
        "TimeSeriesForecast" => new TimeSeriesForecast(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
