using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedClassicStochasticBenchmarks : SharedMappedBenchmarkBase
{
    [Params("TaLib.Functions.Stoch","TaLib.Functions.StochF","TaLib.Functions.StochRsi","Skender.GetStochRsi","Trady.Indicator.StochasticsRsiOscillator")]
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
    protected override IIndicator Create() => Operation switch
    {
        "TaLib.Functions.Stoch" => new ClassicStochastic(),
        "TaLib.Functions.StochF" => new ClassicStochastic(kPeriod:1),
        "TaLib.Functions.StochRsi" => new ClassicStochasticRsi(),
        "Skender.GetStochRsi" => new SmoothedStochasticRsi(20,20,3,1),
        "Trady.Indicator.StochasticsRsiOscillator" => new NullableStochasticRsi(20),
        _ => throw new ArgumentException("Unknown classical stochastic operation.")
    };
}
