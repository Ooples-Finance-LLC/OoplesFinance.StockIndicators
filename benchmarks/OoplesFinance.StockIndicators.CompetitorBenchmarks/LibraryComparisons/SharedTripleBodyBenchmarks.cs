using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTripleBodyBenchmarks : SharedCandleBenchmarkBase
{
    [Params("TaLib.Candles.ThreeInside", "TaLib.Candles.TwoCrows", "TaLib.Candles.UpsideGapTwoCrows", "TaLib.Candles.UniqueThreeRiver")]
    public string Operation { get; set; } = "";
    protected override int Warmup => 12;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _name = Operation.StartsWith("TaLib.Candles.") ? Operation[14..] : throw new ArgumentException("Unknown operation.");
        _ = TripleBodyComparison.Create(_name);
        var fixture = TripleBodyComparison.Fixture().IndicatorBars;
        var (expected, serial) = InitializeFixture(fixture, Operation);
        if (Count >= 1000 && (!serial.Contains(_name is "ThreeInside" or "UniqueThreeRiver" ? 100 : -100) || !serial.Skip(Warmup).Contains(0) || _name == "ThreeInside" && !serial.Contains(-100)))
            throw new InvalidOperationException("Triple-body fixture must include matches and nonmatches.");
        VerifyBuilder(expected, () => TripleBodyComparison.Create(_name));
    }
    [Benchmark] public IIndicatorRun BuilderFull() => Build(TripleBodyComparison.Create(_name), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(TripleBodyComparison.Create(_name), IndicatorHistoryMode.LatestOnly);
    protected override TALib.Core.RetCode Call(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> output, out System.Range range) => _name switch
    {
        "ThreeInside" => Candles.ThreeInside<double>(open, high, low, close, System.Range.All, output, out range),
        "TwoCrows" => Candles.TwoCrows<double>(open, high, low, close, System.Range.All, output, out range),
        "UpsideGapTwoCrows" => Candles.UpsideGapTwoCrows<double>(open, high, low, close, System.Range.All, output, out range),
        "UniqueThreeRiver" => Candles.UniqueThreeRiver<double>(open, high, low, close, System.Range.All, output, out range),
        _ => throw new InvalidOperationException("Unknown triple-body operation.")
    };
}
