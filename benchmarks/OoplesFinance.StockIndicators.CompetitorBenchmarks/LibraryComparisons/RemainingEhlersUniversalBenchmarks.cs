using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingEhlersUniversalBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EhlersUniversalOscillator", "EhlersUniversalTradingFilter", "EhlersSnakeUniversalTradingFilter")]
    public string Operation { get; set; } = "EhlersUniversalOscillator";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EhlersUniversalOscillator" => new EhlersUniversalOscillator(),
        "EhlersUniversalTradingFilter" => new EhlersUniversalTradingFilter(),
        "EhlersSnakeUniversalTradingFilter" => new EhlersSnakeUniversalTradingFilter(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
