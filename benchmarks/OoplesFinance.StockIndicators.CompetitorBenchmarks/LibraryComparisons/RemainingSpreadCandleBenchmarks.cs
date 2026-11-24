using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSpreadCandleBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("RelativeSpreadStrength", "RetrospectiveCandlestickChart")]
    public string Operation { get; set; } = "RelativeSpreadStrength";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "RelativeSpreadStrength" => new RelativeSpreadStrength(),
"RetrospectiveCandlestickChart" => new RetrospectiveCandlestickChart(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
