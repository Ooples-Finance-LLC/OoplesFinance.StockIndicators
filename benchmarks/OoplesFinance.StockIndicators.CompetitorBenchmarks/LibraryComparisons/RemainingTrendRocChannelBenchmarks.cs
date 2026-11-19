using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTrendRocChannelBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TrendTraderBands", "StandardDeviationChannel", "RateOfChangeBands")]
    public string Operation { get; set; } = "TrendTraderBands";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "TrendTraderBands" => new TrendTraderBands(),
        "StandardDeviationChannel" => new StandardDeviationChannel(),
        "RateOfChangeBands" => new RateOfChangeBands(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
