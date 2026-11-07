using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSchaffPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("SchaffTrendCycle", "SchaffTrendCycleShk")]
    public string Operation { get; set; } = "SchaffTrendCycle";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "SchaffTrendCycle" => new SchaffTrendCycle(),
"SchaffTrendCycleShk" => new SchaffTrendCycleShk(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
