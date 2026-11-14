using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingCandlePowerBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("BullPower", "BearPower", "BullPowerIndicator", "BearPowerIndicator")]
    public string Operation { get; set; } = "BullPower";

    protected override double OpenOffset => .5;

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "BullPower" => new BullPower(5),
        "BearPower" => new BearPower(5),
        "BullPowerIndicator" => new BullPowerIndicator(5),
        "BearPowerIndicator" => new BearPowerIndicator(5),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
