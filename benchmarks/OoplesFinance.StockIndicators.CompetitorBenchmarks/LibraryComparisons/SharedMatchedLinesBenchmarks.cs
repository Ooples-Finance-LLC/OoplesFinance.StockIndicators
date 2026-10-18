using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedMatchedLinesBenchmarks : SharedCandleBenchmarkBase
{
    [Params("TaLib.Candles.Counterattack", "TaLib.Candles.SeparatingLines", "TaLib.Candles.GapSideBySideWhiteLines")]
    public string Operation { get; set; } = "";
    protected override int Warmup => _name == "GapSideBySideWhiteLines" ? 7 : 11;
    private IIndicator Create() => MatchedLinesComparison.Create(_name, _name == "GapSideBySideWhiteLines" ? 5 : 10);
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _name = Operation.StartsWith("TaLib.Candles.") ? Operation[14..] : throw new ArgumentException("Unknown operation.");
        if (!MatchedLinesComparison.Names.Contains(_name)) throw new ArgumentException("Unknown matched-lines operation.");
        var fixture = MatchedLinesComparison.Cases.SelectMany(c => c.Name == "GapSideBySideWhiteLines"
            ? c.Bars : c.Bars.Concat(c.Bars.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, 1)))).ToArray();
        var (expected, serial) = InitializeFixture(fixture, Operation);
        if (Count >= 1000 && (!serial.Contains(-100) || !serial.Contains(100) || !serial.Skip(Warmup).Contains(0)))
            throw new InvalidOperationException("Matched-lines fixture must include matches and nonmatches.");
        VerifyBuilder(expected, () => Create());
    }
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    protected override TALib.Core.RetCode Call(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> output, out System.Range range) => _name switch
    {
        "Counterattack" => Candles.Counterattack<double>(open, high, low, close, System.Range.All, output, out range),
        "SeparatingLines" => Candles.SeparatingLines<double>(open, high, low, close, System.Range.All, output, out range),
        "GapSideBySideWhiteLines" => Candles.GapSideBySideWhiteLines<double>(open, high, low, close, System.Range.All, output, out range),
        _ => throw new InvalidOperationException("Unknown matched-lines operation.")
    };
}
