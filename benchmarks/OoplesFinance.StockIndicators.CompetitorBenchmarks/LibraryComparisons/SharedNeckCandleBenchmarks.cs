using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedNeckCandleBenchmarks : SharedCandleBenchmarkBase
{
    [Params("TaLib.Candles.OnNeck", "TaLib.Candles.InNeck", "TaLib.Candles.Thrusting")]
    public string Operation { get; set; } = "";
    protected override int Warmup => 11;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _name = Operation.StartsWith("TaLib.Candles.") ? Operation[14..] : throw new ArgumentException("Unknown operation.");
        _ = NeckCandleComparison.Create(_name);
        var fixture = NeckCandleComparison.Fixture().IndicatorBars;
        var (expected, serial) = InitializeFixture(fixture, Operation);
        if (Count >= 1000 && (!serial.Contains(-100) || !serial.Skip(Warmup).Contains(0)))
            throw new InvalidOperationException("Neck fixture must include matches and nonmatches.");
        VerifyBuilder(expected, () => NeckCandleComparison.Create(_name));
    }
    [Benchmark] public IIndicatorRun BuilderFull() => Build(NeckCandleComparison.Create(_name), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(NeckCandleComparison.Create(_name), IndicatorHistoryMode.LatestOnly);
    protected override TALib.Core.RetCode Call(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> output, out System.Range range) => _name switch
    {
        "OnNeck" => Candles.OnNeck<double>(open, high, low, close, System.Range.All, output, out range),
        "InNeck" => Candles.InNeck<double>(open, high, low, close, System.Range.All, output, out range),
        "Thrusting" => Candles.Thrusting<double>(open, high, low, close, System.Range.All, output, out range),
        _ => throw new InvalidOperationException("Unknown neck operation.")
    };
}
