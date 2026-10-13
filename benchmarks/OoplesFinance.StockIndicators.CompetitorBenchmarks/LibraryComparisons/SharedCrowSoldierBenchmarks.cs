using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Uses the original library-pair benchmark inputs so saved competitor results
// remain useful. Native timing is deliberately not repeated in this fixture.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedCrowSoldierBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("TaLib.Candles.ThreeBlackCrows", "TaLib.Candles.IdenticalThreeCrows", "TaLib.Candles.ThreeWhiteSoldiers")]
    public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private string _name = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        var pair = ComparisonPairs.Get(Operation);
        if (!CrowSoldierComparison.Pairs.Any(p => p.Id == Operation)) throw new ArgumentException("Unknown crow/soldier operation.");
        _name = Operation[14..];
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _bars = data.IndicatorBars;
        ComparisonVerifier.Check(pair, data, 20, verifyIsolation: false);
        var expected = pair.Reference!(data, 20).Outputs["Value"].Values;
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = CrowSoldierComparison.Create(_name);
            using var run = Build(indicator, mode);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator.Outputs[0]].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(CrowSoldierComparison.Create(_name), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(CrowSoldierComparison.Create(_name), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
