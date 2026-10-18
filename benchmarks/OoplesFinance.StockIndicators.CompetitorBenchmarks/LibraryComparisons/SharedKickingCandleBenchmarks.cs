using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedKickingCandleBenchmarks : SharedCandleBenchmarkBase
{
    [Params("TaLib.Candles.Kicking", "TaLib.Candles.KickingByLength")]
    public string Operation { get; set; } = "";
    protected override int Warmup => 11;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _name = Operation.StartsWith("TaLib.Candles.") ? Operation[14..] : throw new ArgumentException("Unknown operation.");
        if (_name is not ("Kicking" or "KickingByLength")) throw new ArgumentException("Unknown kicking operation.");
        var fixture = KickingRickshawComparison.Fixture().IndicatorBars;
        var (expected, serial) = InitializeFixture(fixture, Operation);
        if (Count >= 1000 && (!serial.Contains(-100) || !serial.Contains(100) || !serial.Skip(Warmup).Contains(0)))
            throw new InvalidOperationException("Kicking fixture must include matches and nonmatches.");
        VerifyBuilder(expected, () => KickingRickshawComparison.Create(_name));
    }
    [Benchmark] public IIndicatorRun BuilderFull() => Build(KickingRickshawComparison.Create(_name), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(KickingRickshawComparison.Create(_name), IndicatorHistoryMode.LatestOnly);
    protected override TALib.Core.RetCode Call(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> output, out System.Range range) => _name switch
    {
        "Kicking" => Candles.Kicking<double>(open, high, low, close, System.Range.All, output, out range),
        "KickingByLength" => Candles.KickingByLength<double>(open, high, low, close, System.Range.All, output, out range),
        _ => throw new InvalidOperationException("Unknown kicking operation.")
    };
}
