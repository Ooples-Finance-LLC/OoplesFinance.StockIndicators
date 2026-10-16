using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingContractExtremaBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("ContractHigh", "ContractLow")]
    public string Operation { get; set; } = "ContractHigh";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "ContractHigh" => new ContractHigh(),
        "ContractLow" => new ContractLow(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
