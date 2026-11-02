using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// These variants have separate timings: the published Ma mapping uses SMA.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedClassicalWindowVariantBenchmarks : SharedMappedBenchmarkBase
{
    [Params(ClassicAverageMethod.Wma, ClassicAverageMethod.Trima)]
    public ClassicAverageMethod Method { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (Method is not (ClassicAverageMethod.Wma or ClassicAverageMethod.Trima))
            throw new ArgumentOutOfRangeException(nameof(Method));
        var (_, data) = PrepareFixture("TaLib.Functions.Ma");
        var pair = ClassicAverageComparison.Pair(Method);
        SharedBenchmarkSupport.VerifyMappedOutputs(
            _bars, Create, pair.Reference!(data, 20), pair.OutputNames ?? ["Value"]);
    }

    protected override IIndicator Create() => new ClassicMovingAverage(20, Method);
}
