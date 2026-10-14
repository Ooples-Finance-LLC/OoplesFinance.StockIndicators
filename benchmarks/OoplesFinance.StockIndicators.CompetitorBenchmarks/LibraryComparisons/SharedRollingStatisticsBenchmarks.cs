using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedRollingStatisticsBenchmarks : SharedMappedBenchmarkBase
{
    [Params("Skender.GetBeta","Skender.GetSmaAnalysis","TaLib.Functions.AvgDev","Skender.GetCorrelation",
        "Skender.GetCci","Trady.Indicator.CommodityChannelIndex","Skender.GetEpma")]
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
        "Skender.GetBeta" => new WindowBetaStatistics(20,ReturnBetaSelection.All),
        "Skender.GetSmaAnalysis" => new MovingAverageDiagnostics(20),
        "TaLib.Functions.AvgDev" => new WindowMeanAbsoluteDeviation(20),
        "Skender.GetCorrelation" => new WindowPairStatistics(20),
        "Skender.GetCci" => new WindowCommodityChannelIndex(20),
        "Trady.Indicator.CommodityChannelIndex" => new WindowCommodityChannelIndex(20,true,true),
        "Skender.GetEpma" => new WindowLinearRegression(20,WindowRegressionOutput.Endpoint),
        _ => throw new ArgumentException("Unknown rolling statistics operation.")
    };
}
