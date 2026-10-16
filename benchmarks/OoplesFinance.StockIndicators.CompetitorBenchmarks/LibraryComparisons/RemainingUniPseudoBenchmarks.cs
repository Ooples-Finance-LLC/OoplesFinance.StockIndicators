using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingUniPseudoBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("UniChannel", "PseudoPolynomialChannel")]
    public string Operation { get; set; } = "UniChannel";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "UniChannel" => new UniChannel(),
        "PseudoPolynomialChannel" => new PseudoPolynomialChannel(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
