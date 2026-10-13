using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyStochasticBenchmarks
{
    [Params(1000,10000)] public int Count { get; set; }
    [Params("Trady.Indicator.RawStochasticsValue","Trady.Indicator.Stochastics+Fast","Trady.Indicator.Stochastics+Slow","Trady.Indicator.Stochastics+Full","Trady.Indicator.StochasticsOscillator+Fast","Trady.Indicator.StochasticsOscillator+Slow","Trady.Indicator.StochasticsOscillator+Full","Trady.Indicator.StochasticsMomentum")]
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
                    // Raw stochastic publishes fifty during startup and flat windows.
                    AsinFeasibilityBenchmarks.RequireSame(reference.Values,run[owner.Outputs[slot]].ToArray());
                }
            }
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism=_priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(),IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(),IndicatorHistoryMode.LatestOnly);
    private IIndicator Create()
    {
        if(Operation=="Trady.Indicator.StochasticsMomentum") return new WindowStochasticMomentum(20);
        string variant=Operation=="Trady.Indicator.RawStochasticsValue" ? "Raw"
            : Operation[(Operation.IndexOf('+')+1)..]+(Operation.Contains("Oscillator",StringComparison.Ordinal) ? "Difference" : "");
        return StochasticSmaComparison.Indicator(20,variant,3,3);
    }
    private IIndicatorRun Build(IIndicator owner,IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(owner).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
