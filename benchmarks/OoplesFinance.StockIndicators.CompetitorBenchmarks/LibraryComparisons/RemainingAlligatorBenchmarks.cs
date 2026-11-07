using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingAlligatorBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("AlligatorJaw", "AlligatorTeeth", "AlligatorLips")]
    public string Operation { get; set; } = "AlligatorJaw";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "AlligatorJaw" => new AlligatorJaw(13),
        "AlligatorTeeth" => new AlligatorTeeth(8),
        "AlligatorLips" => new AlligatorLips(5),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
