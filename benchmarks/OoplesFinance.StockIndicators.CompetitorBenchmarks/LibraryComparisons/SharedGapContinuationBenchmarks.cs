using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Uses the original library-pair benchmark inputs so saved competitor results
// remain useful. Native timing is deliberately not repeated in this fixture.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedGapContinuationBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("TaLib.Candles.Tristar", "TaLib.Candles.TasukiGap", "TaLib.Candles.UpDownSideGapThreeMethods")]
    public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private string _name = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        var pair = ComparisonPairs.Get(Operation);
        if (Operation is not ("TaLib.Candles.Tristar" or "TaLib.Candles.TasukiGap" or "TaLib.Candles.UpDownSideGapThreeMethods")) throw new ArgumentException("Unknown gap continuation operation.");
        _name = Operation[14..];
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _bars = data.IndicatorBars;
        ComparisonVerifier.Check(pair, data, 20, verifyIsolation: false);
        var expected = pair.Reference!(data, 20).Outputs["Value"].Values;
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = GapContinuationComparison.Create(_name, _name == "TasukiGap" ? 5 : 10);
            using var run = Build(indicator, mode);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator.Outputs[0]].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(GapContinuationComparison.Create(_name, _name == "TasukiGap" ? 5 : 10), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(GapContinuationComparison.Create(_name, _name == "TasukiGap" ? 5 : 10), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
