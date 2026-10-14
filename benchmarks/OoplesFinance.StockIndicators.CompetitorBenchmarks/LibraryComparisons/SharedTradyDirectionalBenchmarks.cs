using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyDirectionalBenchmarks
{
    [Params(1000,10000)] public int Count { get; set; }
    [Params("Trady.Indicator.PlusDirectionalMovement","Trady.Indicator.MinusDirectionalMovement",
        "Trady.Indicator.PlusDirectionalIndicator","Trady.Indicator.MinusDirectionalIndicator",
        "Trady.Indicator.DirectionalMovementIndex","Trady.Indicator.AverageDirectionalIndex","Trady.Indicator.AverageDirectionalIndexRating")]
    public string Operation { get; set; } = "";
    private Bar[] _bars=null!;
    private DirectionalWindowMeasure _measure;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop=CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism=8;
        int index=Array.FindIndex(DirectionalComparison.Pairs,p => p.Id==Operation);
        if (index<0) throw new ArgumentException("Unknown directional operation.");
        _measure=(DirectionalWindowMeasure)index;
        var pair=DirectionalComparison.Pairs[index];
        _bars=ComparisonVerifier.BenchmarkFixture(pair,Count).IndicatorBars;
        // Keep native execution out of setup; existing golden tests qualify its
        // decimal contract separately from this exact binary64 reference.
        var expected=DirectionalComparison.Reference(_bars,20,20)[index];
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner=Create();
            using var run=Build(owner,mode);
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v??0).ToArray(),run[owner.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v.HasValue ? 1d : 0d).ToArray(),run[owner.IsDefined].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism=_priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(),IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(),IndicatorHistoryMode.LatestOnly);
    private WindowDirectionalMeasure Create() => new(_measure,20,20);
    private IIndicatorRun Build(WindowDirectionalMeasure owner,IndicatorHistoryMode mode) => SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
}
