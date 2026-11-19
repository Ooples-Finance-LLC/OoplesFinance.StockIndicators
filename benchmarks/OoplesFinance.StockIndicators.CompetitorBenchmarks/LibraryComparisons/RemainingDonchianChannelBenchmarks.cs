using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingDonchianChannelBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("DonchianChannel", "DonchianChannelUpper", "DonchianChannelLower")]
    public string Operation { get; set; } = "DonchianChannel";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "DonchianChannel" => new DonchianChannel(),
        "DonchianChannelUpper" => new DonchianChannelUpper(),
        "DonchianChannelLower" => new DonchianChannelLower(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
