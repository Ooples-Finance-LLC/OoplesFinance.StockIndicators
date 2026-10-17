using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMoneyFlowPercentBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("VolumeAccumulationPercent", "TwiggsMoneyFlow")]
    public string Operation { get; set; } = "VolumeAccumulationPercent";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "VolumeAccumulationPercent" => new VolumeAccumulationPercent(),
        "TwiggsMoneyFlow" => new TwiggsMoneyFlow(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
