using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedStrengthBenchmarks
{
    [Params(1000,10000)] public int Count { get; set; }
    [Params("Skender.GetRsi","Skender.GetAdx","Trady.Indicator.RelativeStrength","Trady.Indicator.RelativeStrengthIndex","Trady.Indicator.NetMomentumOscillator","Trady.Indicator.RelativeMomentum","Trady.Indicator.RelativeMomentumIndex")]
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
        var expected=pair.Reference!(data,20);
        var names=pair.OutputNames ?? ["Value"];
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
    private IIndicator Create()
    {
        if(Operation=="Skender.GetRsi") return new WilderStrengthOscillator(20,WilderStrengthConvention.RsiHundredFlat);
        if(Operation=="Skender.GetAdx") return new SumSeededDirectionalIndex(20);
        string name=Operation["Trady.Indicator.".Length..];
        var convention=name=="NetMomentumOscillator" ? NullableStrengthConvention.NetMomentum : Enum.Parse<NullableStrengthConvention>(name);
        return new NullableStrengthOscillator(20,convention,NullableStrengthComparison.Momentum(convention) ? 5 : 1);
    }
    private IIndicatorRun Build(IIndicator owner,IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(owner).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
