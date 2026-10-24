using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingGainLossPairBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("IntradayMomentumIndex", "ChandeIntradayMomentumIndex", "RelativeMomentumIndex")]
    public string Operation { get; set; } = "IntradayMomentumIndex";

    protected override double GetOpen(int index, double price) => price + (index % 3 == 0 ? .5 : -.25);

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "IntradayMomentumIndex" => new IntradayMomentumIndex(5),
        "ChandeIntradayMomentumIndex" => new ChandeIntradayMomentumIndex(5),
        "RelativeMomentumIndex" => new RelativeMomentumIndex(14, 3),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
