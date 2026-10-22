using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingVervoortCandlesBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("VervoortHeikenAshiCandlestickOscillator", "VervoortHeikenAshiLongTermCandlestickOscillator", "VervoortSmoothedOscillator")]
    public string Operation { get; set; } = "VervoortHeikenAshiCandlestickOscillator";
    protected override DateTime GetTime(int index) => new DateTime(2026, 1, 1).AddHours(index);
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "VervoortHeikenAshiCandlestickOscillator" => new VervoortHeikenAshiCandlestickOscillator(34),
        "VervoortHeikenAshiLongTermCandlestickOscillator" => new VervoortHeikenAshiLongTermCandlestickOscillator(55),
        "VervoortSmoothedOscillator" => new VervoortSmoothedOscillator(18, length1: 18, length2: 30, length3: 2, smoothLength: 3, stdDevMult: 2),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
