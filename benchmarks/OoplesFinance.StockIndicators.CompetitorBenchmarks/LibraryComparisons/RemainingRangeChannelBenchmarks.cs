using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRangeChannelBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AverageTrueRangeChannel", "StollerAverageRangeChannels", "KeltnerChannelWidth")]
    public string Operation { get; set; } = "AverageTrueRangeChannel";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AverageTrueRangeChannel" => new AverageTrueRangeChannel(),
        "StollerAverageRangeChannels" => new StollerAverageRangeChannels(),
        "KeltnerChannelWidth" => new KeltnerChannelWidth(20, OoplesFinance.StockIndicators.Enums.MovingAvgType.ExponentialMovingAverage),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
