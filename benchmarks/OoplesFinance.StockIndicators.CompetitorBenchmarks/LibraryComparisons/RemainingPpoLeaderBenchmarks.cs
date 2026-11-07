using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPpoLeaderBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("PercentagePriceOscillatorLeader")]
    public string Operation { get; set; } = "PercentagePriceOscillatorLeader";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "PercentagePriceOscillatorLeader" => new PercentagePriceOscillatorLeader(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
