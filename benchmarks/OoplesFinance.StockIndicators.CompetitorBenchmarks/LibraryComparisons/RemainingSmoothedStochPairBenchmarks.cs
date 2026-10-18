using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSmoothedStochPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("DoubleSmoothedStochastic", "DTOscillator")]
    public string Operation { get; set; } = "DoubleSmoothedStochastic";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "DoubleSmoothedStochastic" => new DoubleSmoothedStochastic(),
        "DTOscillator" => new DTOscillator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
