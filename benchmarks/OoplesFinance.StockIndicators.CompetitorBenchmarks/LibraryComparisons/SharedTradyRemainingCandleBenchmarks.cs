using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Original library-pair inputs permit reuse of saved native timing.
// Three-method patterns retain unbounded anchor history in shared sequential state.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyRemainingCandleBenchmarks
{
    [Params(1000,10000)] public int Count { get; set; }
    [Params("Trady.Candlestick.DarkCloudCover", "Trady.Candlestick.RisingThreeMethods", "Trady.Candlestick.FallingThreeMethods", "Trady.Candlestick.DragonifyDoji")]
    public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop=CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism=8;
        var pair=ComparisonPairs.Get(Operation);
        var data=ComparisonVerifier.BenchmarkFixture(pair,Count);
        _bars=data.IndicatorBars;
        // Golden tests check the native contract separately. Do not repeat the
        // expensive native backward scan in each builder benchmark setup.
        var expected=pair.Reference!(data,20).Outputs["Value"].Values;
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner=Create();
            using var run=Build(owner,mode);
            AsinFeasibilityBenchmarks.RequireSame(expected,run[owner.Outputs[0]].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism=_priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(),IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(),IndicatorHistoryMode.LatestOnly);
    private IIndicator Create() => Operation switch
    {
        "Trady.Candlestick.DarkCloudCover" => new DelayedDarkCloudCoverPattern(20,3),
        "Trady.Candlestick.RisingThreeMethods" => new RisingThreeMethodsPattern(20,20),
        "Trady.Candlestick.FallingThreeMethods" => new FallingThreeMethodsPattern(20,20),
        "Trady.Candlestick.DragonifyDoji" => new DragonflyDojiCandle(),
        _ => throw new ArgumentException("Unknown Trady candle operation.")
    };
    private IIndicatorRun Build(IIndicator owner, IndicatorHistoryMode mode) => SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
}
