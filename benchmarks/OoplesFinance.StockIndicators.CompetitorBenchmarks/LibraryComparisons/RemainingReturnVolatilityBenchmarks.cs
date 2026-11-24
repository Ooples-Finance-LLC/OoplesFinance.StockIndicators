using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingReturnVolatilityBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("CloseToCloseVolatility", "DownsideDeviation")]
    public string Operation { get; set; } = "CloseToCloseVolatility";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "CloseToCloseVolatility" => new CloseToCloseVolatility(),
        "DownsideDeviation" => new DownsideDeviation(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
