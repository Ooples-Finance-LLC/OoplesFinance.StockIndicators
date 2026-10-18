using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedContextReversalBenchmarks : SharedCandleBenchmarkBase
{
    [Params("TaLib.Candles.Hammer", "TaLib.Candles.HangingMan", "TaLib.Candles.InvertedHammer", "TaLib.Candles.ShootingStar")]
    public string Operation { get; set; } = "";
    protected override int Warmup => 11;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _name = Operation.StartsWith("TaLib.Candles.") ? Operation[14..] : throw new ArgumentException("Unknown operation.");
        _ = ContextReversalComparison.Create(_name);
        var fixture = ContextReversalComparison.Fixture().IndicatorBars;
        var (expected, serial) = InitializeFixture(fixture, Operation);
        if (Count >= 1000 && (!serial.Contains(_name is "Hammer" or "InvertedHammer" ? 100 : -100) || !serial.Skip(Warmup).Contains(0)))
            throw new InvalidOperationException("Context-reversal fixture must include matches and nonmatches.");
        VerifyBuilder(expected, () => ContextReversalComparison.Create(_name));
    }
    [Benchmark] public IIndicatorRun BuilderFull() => Build(ContextReversalComparison.Create(_name), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(ContextReversalComparison.Create(_name), IndicatorHistoryMode.LatestOnly);
    protected override TALib.Core.RetCode Call(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> output, out System.Range range) => _name switch
    {
        "Hammer" => Candles.Hammer<double>(open, high, low, close, System.Range.All, output, out range),
        "HangingMan" => Candles.HangingMan<double>(open, high, low, close, System.Range.All, output, out range),
        "InvertedHammer" => Candles.InvertedHammer<double>(open, high, low, close, System.Range.All, output, out range),
        "ShootingStar" => Candles.ShootingStar<double>(open, high, low, close, System.Range.All, output, out range),
        _ => throw new InvalidOperationException("Unknown context reversal operation.")
    };
}
