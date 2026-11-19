using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingScalperHurstBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ScalpersChannel", "HurstCycleChannel", "HurstBands")]
    public string Operation { get; set; } = "ScalpersChannel";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "ScalpersChannel" => new ScalpersChannel(),
        "HurstCycleChannel" => new HurstCycleChannel(),
        "HurstBands" => new HurstBands(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
