using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAbsoluteErrorBandBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MeanAbsoluteErrorBands", "MeanAbsoluteDeviationBands")]
    public string Operation { get; set; } = "MeanAbsoluteErrorBands";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "MeanAbsoluteErrorBands" => new MeanAbsoluteErrorBands(),
        "MeanAbsoluteDeviationBands" => new MeanAbsoluteDeviationBands(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
