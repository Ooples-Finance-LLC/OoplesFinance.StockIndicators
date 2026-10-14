using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedContainmentCandleBenchmarks : SharedCandleBenchmarkBase
{
    [Params("TaLib.Candles.Harami", "TaLib.Candles.HaramiCross", "TaLib.Candles.HomingPigeon", "TaLib.Candles.DojiStar")]
    public string Operation { get; set; } = "";
    protected override int Warmup => 11;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _name = Operation.StartsWith("TaLib.Candles.") ? Operation[14..] : throw new ArgumentException("Unknown operation.");
        _ = ContainmentCandleComparison.Create(_name);
        var fixture = ContainmentCandleComparison.Fixture().IndicatorBars;
        var (expected, serial) = InitializeFixture(fixture, Operation);
        if (Count >= 1000 && (!serial.Contains(100) || !serial.Skip(Warmup).Contains(0) || _name != "HomingPigeon" && !serial.Contains(-100)))
            throw new InvalidOperationException("Containment fixture must include matches and nonmatches.");
        VerifyBuilder(expected, () => ContainmentCandleComparison.Create(_name));
    }
    [Benchmark] public IIndicatorRun BuilderFull() => Build(ContainmentCandleComparison.Create(_name), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(ContainmentCandleComparison.Create(_name), IndicatorHistoryMode.LatestOnly);
    protected override TALib.Core.RetCode Call(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> output, out System.Range range) => _name switch
    {
        "Harami" => Candles.Harami<double>(open, high, low, close, System.Range.All, output, out range),
        "HaramiCross" => Candles.HaramiCross<double>(open, high, low, close, System.Range.All, output, out range),
        "HomingPigeon" => Candles.HomingPigeon<double>(open, high, low, close, System.Range.All, output, out range),
        "DojiStar" => Candles.DojiStar<double>(open, high, low, close, System.Range.All, output, out range),
        _ => throw new InvalidOperationException("Unknown containment operation.")
    };
}
