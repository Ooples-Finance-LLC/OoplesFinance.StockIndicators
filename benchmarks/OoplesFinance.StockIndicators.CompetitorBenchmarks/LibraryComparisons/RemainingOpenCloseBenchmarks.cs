using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingOpenCloseBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ChandeQuickStick", "DeltaMovingAverage")]
    public string Operation { get; set; } = "ChandeQuickStick";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override double GetOpen(int index, double price) => price + (index % 3 - 1) * 0.75;

    protected override IIndicator Create() => Operation switch
    {
        "ChandeQuickStick" => new ChandeQuickStick(),
        "DeltaMovingAverage" => new DeltaMovingAverage(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
