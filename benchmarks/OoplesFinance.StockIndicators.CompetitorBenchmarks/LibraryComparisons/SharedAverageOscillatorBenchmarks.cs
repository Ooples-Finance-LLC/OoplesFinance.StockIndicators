using System.Numerics;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedAverageOscillatorBenchmarks
{
    [Params(1000,10000)] public int Count { get; set; }
    [Params("Trady.Indicator.SimpleMovingAverageOscillator","Trady.Indicator.ExponentialMovingAverageOscillator",
        "Trady.Indicator.ExponentialMovingAverage","Trady.Indicator.WeightedMovingAverage","Skender.GetAwesome")]
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
        var expected=Operation is "Trady.Indicator.ExponentialMovingAverage" or "Trady.Indicator.WeightedMovingAverage"
            ? ScalarReference(Operation.EndsWith(".ExponentialMovingAverage",StringComparison.Ordinal)) : pair.Reference!(data,20);
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
    private IIndicator Create() => Operation switch
    {
        "Trady.Indicator.SimpleMovingAverageOscillator" => new MovingAverageDifference(),
        "Trady.Indicator.ExponentialMovingAverageOscillator" => new MovingAverageDifference(exponential:true),
        "Trady.Indicator.ExponentialMovingAverage" => new FirstValueEma(20),
        "Trady.Indicator.WeightedMovingAverage" => new Wma(20),
        "Skender.GetAwesome" => new AwesomeWithDetails(),
        _ => throw new ArgumentException("Unknown average oscillator operation.")
    };
    private ComparisonSeries ScalarReference(bool exponential)
    {
        var values=new double[_bars.Length];
        for (int i=0;i<values.Length;i++)
        {
            if (exponential)
                values[i]=i==0 ? _bars[i].Close : Round(2*Units(_bars[i].Close)+19*Units(values[i-1]),21*Grid);
            else
            {
                int n=Math.Min(20,i+1); var sum=BigInteger.Zero;
                for (int j=0;j<n;j++) sum+=(j+1)*Units(_bars[i-n+1+j].Close);
                values[i]=Round(sum,Grid*n*(n+1)/2);
            }
        }
        // The library-pair EMA reference is a native decimal recurrence. The
        // builder rounds binary64 at every stage; verify that exact contract.
        return new(exponential ? 0 : Math.Min(19,_bars.Length),values);
    }
    private IIndicatorRun Build(IIndicator owner,IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(owner).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
