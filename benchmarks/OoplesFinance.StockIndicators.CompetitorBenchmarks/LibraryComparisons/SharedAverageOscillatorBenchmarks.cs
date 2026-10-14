using System.Numerics;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedAverageOscillatorBenchmarks : SharedMappedBenchmarkBase
{
    [Params("Trady.Indicator.SimpleMovingAverageOscillator","Trady.Indicator.ExponentialMovingAverageOscillator",
        "Trady.Indicator.ExponentialMovingAverage","Trady.Indicator.WeightedMovingAverage","Skender.GetAwesome")]
    public string Operation { get; set; } = "";
    [GlobalSetup]
    public void Setup()
    {
        var (pair,data)=PrepareFixture(Operation);
        // Saved native timing is reused. Independent exact references validate
        // the builder; existing golden tests retain each native arithmetic contract.
        var expected=Operation is "Trady.Indicator.ExponentialMovingAverage" or "Trady.Indicator.WeightedMovingAverage"
            ? ScalarReference(Operation.EndsWith(".ExponentialMovingAverage",StringComparison.Ordinal)) : pair.Reference!(data,20);
        var names=pair.OutputNames ?? ["Value"];
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, expected, names);
    }
    protected override IIndicator Create() => Operation switch
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
}
