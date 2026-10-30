using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRSeriesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ReallySimpleIndicator", "RexOscillator", "RelativeDifferenceOfSquaresOscillator", "RecursiveDifferenciator")]
    public string Operation { get; set; } = "ReallySimpleIndicator";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ReallySimpleIndicator" => new ReallySimpleIndicator(),
"RexOscillator" => new RexOscillator(),
"RelativeDifferenceOfSquaresOscillator" => new RelativeDifferenceOfSquaresOscillator(),
"RecursiveDifferenciator" => new RecursiveDifferenciator(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
