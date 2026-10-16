using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Uses the original library-pair benchmark inputs so saved competitor results
// remain useful. Native timing is deliberately not repeated in this fixture.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedStarReversalBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("TaLib.Candles.MorningStar", "TaLib.Candles.EveningStar", "TaLib.Candles.MorningDojiStar", "TaLib.Candles.EveningDojiStar", "TaLib.Candles.AbandonedBaby")]
    public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private string _name = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        var pair = ComparisonPairs.Get(Operation);
        if (!StarReversalComparison.Pairs.Any(p => p.Id == Operation) && Operation != "TaLib.Candles.AbandonedBaby") throw new ArgumentException("Unknown star reversal operation.");
        _name = Operation[14..];
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _bars = data.IndicatorBars;
        ComparisonVerifier.Check(pair, data, 20, verifyIsolation: false);
        var expected = pair.Reference!(data, 20).Outputs["Value"].Values;
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, mode);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator.Outputs[0]].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    private IIndicator Create() => _name == "AbandonedBaby" ? new AbandonedBabyCandle() : StarReversalComparison.Create(_name);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode mode) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, mode);
}
