using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyStochasticBenchmarks : SharedMappedBenchmarkBase
{
    [Params("Trady.Indicator.RawStochasticsValue","Trady.Indicator.Stochastics+Fast","Trady.Indicator.Stochastics+Slow","Trady.Indicator.Stochastics+Full","Trady.Indicator.StochasticsOscillator+Fast","Trady.Indicator.StochasticsOscillator+Slow","Trady.Indicator.StochasticsOscillator+Full","Trady.Indicator.StochasticsMomentum")]
    public string Operation { get; set; } = "";
    [GlobalSetup]
    public void Setup()
    {
        var (pair,data)=PrepareFixture(Operation);
        // Saved native timing is reused. Independent exact references validate
        // the builder; existing golden tests retain each native arithmetic contract.
        var expected=pair.Reference!(data,20);
        var names=pair.OutputNames ?? ["Value"];
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, expected, names, includeScalarStartup: true);
    }
    protected override IIndicator Create()
    {
        if(Operation=="Trady.Indicator.StochasticsMomentum") return new WindowStochasticMomentum(20);
        string variant=Operation=="Trady.Indicator.RawStochasticsValue" ? "Raw"
            : Operation[(Operation.IndexOf('+')+1)..]+(Operation.Contains("Oscillator",StringComparison.Ordinal) ? "Difference" : "");
        return StochasticSmaComparison.Indicator(20,variant,3,3);
    }
}
