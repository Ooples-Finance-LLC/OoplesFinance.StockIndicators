using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedSmoothedStochasticBenchmarks : SharedMappedBenchmarkBase
{
    [Params("Skender.GetStoch","Skender.GetSmi","Trady.Indicator.StochasticsMomentumIndex")]
    public string Operation { get; set; } = "";
    [GlobalSetup]
    public void Setup()
    {
        var (pair,data)=PrepareFixture(Operation);
        // Saved native timing is reused. Independent exact references validate
        // the builder; existing golden tests retain each native arithmetic contract.
        // Trady publishes only the index, but the owned run retains its signal too.
        var referencePair=Operation=="Trady.Indicator.StochasticsMomentumIndex" ? StochasticMomentumComparison.Create("Skender") : pair;
        var expected=referencePair.Reference!(data,20);
        var names=referencePair.OutputNames!;
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, expected, names);
    }
    protected override IIndicator Create() => Operation=="Skender.GetStoch"
        ? new WindowStochasticKdj(14,3,3) : new DoubleSmoothedStochasticMomentum(20,25,2,3);
}
