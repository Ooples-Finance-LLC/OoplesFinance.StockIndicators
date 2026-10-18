using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRangeDojiBenchmarks : SharedCandleBenchmarkBase
{
    [Params("TaLib.Candles.Doji", "TaLib.Candles.DragonflyDoji", "TaLib.Candles.GravestoneDoji", "TaLib.Candles.LongLeggedDoji", "TaLib.Candles.TakuriLine")]
    public string Operation { get; set; } = "";
    protected override int Warmup => _name == "DragonflyDoji" ? 11 : 10;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _name = Operation.StartsWith("TaLib.Candles.") ? Operation[14..] : throw new ArgumentException("Unknown operation.");
        _ = AverageRangeDojiComparison.Create(_name);
        var fixture = AverageRangeDojiComparison.Fixture().IndicatorBars;
        var (expected, serial) = InitializeFixture(fixture, Operation);
        if (Count >= 1000 && (!serial.Contains(100) || !serial.Skip(Warmup).Contains(0)))
            throw new InvalidOperationException("Range-doji fixture must include matches and nonmatches.");
        VerifyBuilder(expected, () => AverageRangeDojiComparison.Create(_name));
    }
    [Benchmark] public IIndicatorRun BuilderFull() => Build(AverageRangeDojiComparison.Create(_name), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(AverageRangeDojiComparison.Create(_name), IndicatorHistoryMode.LatestOnly);
    protected override TALib.Core.RetCode Call(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> output, out System.Range range) => _name switch
    {
        "Doji" => Candles.Doji<double>(open, high, low, close, System.Range.All, output, out range),
        "DragonflyDoji" => Candles.DragonflyDoji<double>(open, high, low, close, System.Range.All, output, out range),
        "GravestoneDoji" => Candles.GravestoneDoji<double>(open, high, low, close, System.Range.All, output, out range),
        "LongLeggedDoji" => Candles.LongLeggedDoji<double>(open, high, low, close, System.Range.All, output, out range),
        "TakuriLine" => Candles.TakuriLine<double>(open, high, low, close, System.Range.All, output, out range),
        _ => throw new InvalidOperationException("Unknown doji operation.")
    };
}
