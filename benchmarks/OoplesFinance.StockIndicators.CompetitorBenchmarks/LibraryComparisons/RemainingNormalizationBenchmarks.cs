using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingNormalizationBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("BayesianOscillator", "AverageAbsoluteErrorNormalization", "BelkhayateTiming")]
    public string Operation { get; set; } = "BayesianOscillator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "BayesianOscillator" => new BayesianOscillator(20),
        "AverageAbsoluteErrorNormalization" => new AverageAbsoluteErrorNormalization(20),
        "BelkhayateTiming" => new BelkhayateTiming(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
