using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingZeroLagCycleBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ZeroLagSmoothedCycle")]
    public string Operation { get; set; } = "ZeroLagSmoothedCycle";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "ZeroLagSmoothedCycle" => new ZeroLagSmoothedCycle(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
