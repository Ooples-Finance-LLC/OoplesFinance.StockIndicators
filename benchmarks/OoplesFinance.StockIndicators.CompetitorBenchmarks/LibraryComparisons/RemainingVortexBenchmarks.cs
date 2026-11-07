using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVortexBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("VortexPositive", "VortexNegative", "VortexPlus", "VortexMinus", "VortexIndicatorPlus", "VortexIndicatorMinus")]
    public string Operation { get; set; } = "VortexPositive";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "VortexPositive" => new VortexPositive(),
        "VortexNegative" => new VortexNegative(),
        "VortexPlus" => new VortexPlus(),
        "VortexMinus" => new VortexMinus(),
        "VortexIndicatorPlus" => new VortexIndicatorPlus(),
        "VortexIndicatorMinus" => new VortexIndicatorMinus(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
