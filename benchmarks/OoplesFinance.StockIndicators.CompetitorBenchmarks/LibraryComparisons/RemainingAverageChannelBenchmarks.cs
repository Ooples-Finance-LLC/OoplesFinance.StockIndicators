using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAverageChannelBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MovingAverageSupportResistance", "MovingAverageChannel", "HighLowBands")]
    public string Operation { get; set; } = "MovingAverageSupportResistance";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "MovingAverageSupportResistance" => new MovingAverageSupportResistance(),
        "MovingAverageChannel" => new MovingAverageChannel(),
        "HighLowBands" => new HighLowBands(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
