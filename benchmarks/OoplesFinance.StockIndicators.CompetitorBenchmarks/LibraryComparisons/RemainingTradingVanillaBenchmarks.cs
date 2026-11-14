using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingTradingVanillaBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("TradingMadeMoreSimplerOscillator", "VanillaABCDPattern")]
    public string Operation { get; set; } = "TradingMadeMoreSimplerOscillator";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "TradingMadeMoreSimplerOscillator" => new TradingMadeMoreSimplerOscillator(14),
        "VanillaABCDPattern" => new VanillaABCDPattern(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
