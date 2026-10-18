using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSequenceCandleBenchmarks : SharedCandleBenchmarkBase
{
    [Params("TaLib.Candles.MatchingLow", "TaLib.Candles.StickSandwich", "TaLib.Candles.ThreeLineStrike", "TaLib.Candles.ThreeOutside")]
    public string Operation { get; set; } = "";
    protected override int Warmup => SequenceCandleComparison.Lookback(_name);
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _name = Operation.StartsWith("TaLib.Candles.") ? Operation[14..] : throw new ArgumentException("Unknown operation.");
        _ = SequenceCandleComparison.Create(_name);
        var fixture = SequenceCandleComparison.Fixture().IndicatorBars;
        var (expected, serial) = InitializeFixture(fixture, Operation);
        if (Count >= 1000 && (!serial.Contains(100) || !serial.Skip(Warmup).Contains(0) || (_name is "ThreeLineStrike" or "ThreeOutside") && !serial.Contains(-100)))
            throw new InvalidOperationException("Sequence fixture must include matches and nonmatches.");
        VerifyBuilder(expected, () => SequenceCandleComparison.Create(_name));
    }
    [Benchmark] public IIndicatorRun BuilderFull() => Build(SequenceCandleComparison.Create(_name), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(SequenceCandleComparison.Create(_name), IndicatorHistoryMode.LatestOnly);
    protected override TALib.Core.RetCode Call(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> output, out System.Range range) => _name switch
    {
        "MatchingLow" => Candles.MatchingLow<double>(open, high, low, close, System.Range.All, output, out range),
        "StickSandwich" => Candles.StickSandwich<double>(open, high, low, close, System.Range.All, output, out range),
        "ThreeLineStrike" => Candles.ThreeLineStrike<double>(open, high, low, close, System.Range.All, output, out range),
        "ThreeOutside" => Candles.ThreeOutside<double>(open, high, low, close, System.Range.All, output, out range),
        _ => throw new InvalidOperationException("Unknown sequence operation.")
    };
}
