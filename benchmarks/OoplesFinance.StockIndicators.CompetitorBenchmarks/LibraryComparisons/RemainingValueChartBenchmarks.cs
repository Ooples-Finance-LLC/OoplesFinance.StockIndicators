using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingValueChartBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ValueChartIndicator")]
    public string Operation { get; set; } = "ValueChartIndicator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "ValueChartIndicator" => new ValueChartIndicator(5, 5),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
