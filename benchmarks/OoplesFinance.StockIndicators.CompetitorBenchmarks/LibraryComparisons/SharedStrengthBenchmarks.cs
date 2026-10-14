using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedStrengthBenchmarks : SharedMappedBenchmarkBase
{
    [Params("Skender.GetRsi","Skender.GetAdx","Trady.Indicator.RelativeStrength","Trady.Indicator.RelativeStrengthIndex","Trady.Indicator.NetMomentumOscillator","Trady.Indicator.RelativeMomentum","Trady.Indicator.RelativeMomentumIndex")]
    public string Operation { get; set; } = "";
    [GlobalSetup]
    public void Setup()
    {
        var (pair,data)=PrepareFixture(Operation);
        // Saved native timing is reused. Independent exact references validate
        // the builder; existing golden tests retain each native arithmetic contract.
        var expected=pair.Reference!(data,20);
        var names=pair.OutputNames ?? ["Value"];
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, expected, names);
    }
    protected override IIndicator Create()
    {
        if(Operation=="Skender.GetRsi") return new WilderStrengthOscillator(20,WilderStrengthConvention.RsiHundredFlat);
        if(Operation=="Skender.GetAdx") return new SumSeededDirectionalIndex(20);
        string name=Operation["Trady.Indicator.".Length..];
        var convention=name=="NetMomentumOscillator" ? NullableStrengthConvention.NetMomentum : Enum.Parse<NullableStrengthConvention>(name);
        return new NullableStrengthOscillator(20,convention,NullableStrengthComparison.Momentum(convention) ? 5 : 1);
    }
}
