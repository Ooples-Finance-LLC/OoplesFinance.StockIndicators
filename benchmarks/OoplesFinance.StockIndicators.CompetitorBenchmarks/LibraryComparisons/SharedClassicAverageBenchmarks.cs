using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedClassicAverageBenchmarks : SharedMappedBenchmarkBase
{
    [Params("TaLib.Functions.Ma","TaLib.Functions.Apo","TaLib.Functions.Ppo")]
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
        "TaLib.Functions.Ma" => new ClassicMovingAverage(20),
        "TaLib.Functions.Apo" => new ClassicPriceOscillator(),
        "TaLib.Functions.Ppo" => new ClassicPriceOscillator(percentage:true),
        _ => throw new ArgumentException("Unknown classical average operation.")
    };
}
