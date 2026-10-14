using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedSmoothedStochasticBenchmarks
{
    [Params(1000,10000)] public int Count { get; set; }
    [Params("Skender.GetStoch","Skender.GetSmi","Trady.Indicator.StochasticsMomentumIndex")]
    public string Operation { get; set; } = "";
    private Bar[] _bars=null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop=CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism=8;
        var pair=ComparisonPairs.Get(Operation);
        var data=ComparisonVerifier.BenchmarkFixture(pair,Count);
        _bars=data.IndicatorBars;
        // Saved native timing is reused. Independent exact references validate
        // the builder; existing golden tests retain each native arithmetic contract.
        // Trady publishes only the index, but the owned run retains its signal too.
        var referencePair=Operation=="Trady.Indicator.StochasticsMomentumIndex" ? StochasticMomentumComparison.Create("Skender") : pair;
        var expected=referencePair.Reference!(data,20);
        var names=referencePair.OutputNames!;
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner=Create(); using var run=Build(owner,mode);
            bool masks=owner.Outputs.Count==2*names.Length;
            for (int slot=0;slot<names.Length;slot++)
            {
                var reference=expected.Outputs[names[slot]];
                if (masks)
                {
                    var present=reference.Present ?? reference.Values.Select((v,i) => i>=reference.FirstValid && !double.IsNaN(v)).ToArray();
                    AsinFeasibilityBenchmarks.RequireSame(reference.Values.Select((v,i) => present[i] ? v : 0).ToArray(),run[owner.Outputs[slot]].ToArray());
                    AsinFeasibilityBenchmarks.RequireSame(present.Select(p => p ? 1d : 0d).ToArray(),run[owner.Outputs[slot+names.Length]].ToArray());
                }
                else
                {
                    // Native scalar comparisons suppress startup; unit tests
                    // independently cover the builder's expanding-prefix values.
                    AsinFeasibilityBenchmarks.RequireSame(reference.Values.Skip(reference.FirstValid).ToArray(),run[owner.Outputs[slot]].ToArray().Skip(reference.FirstValid).ToArray());
                }
            }
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism=_priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(),IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(),IndicatorHistoryMode.LatestOnly);
    private IIndicator Create() => Operation=="Skender.GetStoch"
        ? new WindowStochasticKdj(14,3,3) : new DoubleSmoothedStochasticMomentum(20,25,2,3);
    private IIndicatorRun Build(IIndicator owner,IndicatorHistoryMode mode) => SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
}
