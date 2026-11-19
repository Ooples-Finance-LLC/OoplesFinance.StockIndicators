using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPriceChannelBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("PriceChannel", "PriceChannelMiddle", "PriceChannelUpper", "PriceChannelLower", "PriceLineChannel", "PriceCurveChannel")]
    public string Operation { get; set; } = "PriceChannel";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "PriceChannel" => new PriceChannel(),
        "PriceChannelMiddle" => new PriceChannelMiddle(),
        "PriceChannelUpper" => new PriceChannelUpper(),
        "PriceChannelLower" => new PriceChannelLower(),
        "PriceLineChannel" => new PriceLineChannel(),
        "PriceCurveChannel" => new PriceCurveChannel(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
