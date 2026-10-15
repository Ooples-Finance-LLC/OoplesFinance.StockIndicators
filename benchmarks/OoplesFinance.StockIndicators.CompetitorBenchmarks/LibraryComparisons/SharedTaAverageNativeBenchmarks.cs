using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTaAverageNativeBenchmarks : SharedTaPackedBenchmarkBase
{
    [Params("TaLib.Functions.Ma", "TaLib.Functions.Apo", "TaLib.Functions.Ppo")]
    public string Operation { get; set; } = "";
    protected override string Mapping => Operation;
    protected override int Lookback => Operation == "TaLib.Functions.Ma" ? 19 : 25;
    protected override int Columns => 1;
    protected override IIndicator Create() => Operation switch
    {
        "TaLib.Functions.Ma" => new ClassicMovingAverage(20),
        "TaLib.Functions.Apo" => new ClassicPriceOscillator(),
        "TaLib.Functions.Ppo" => new ClassicPriceOscillator(percentage: true),
        _ => throw new ArgumentException("Unknown TA average operation.")
    };
    protected override TALib.Core.RetCode Call(System.Range input, double[][] output, out System.Range range) => Operation switch
    {
        "TaLib.Functions.Ma" => Functions.Ma<double>(Data.Closes, input,
            output[0], out range, 20, TALib.Core.MAType.Sma),
        "TaLib.Functions.Apo" => Functions.Apo<double>(Data.Closes, input,
            output[0], out range, 12, 26, TALib.Core.MAType.Sma),
        "TaLib.Functions.Ppo" => Functions.Ppo<double>(Data.Closes, input,
            output[0], out range, 12, 26, TALib.Core.MAType.Sma),
        _ => throw new ArgumentException("Unknown TA average operation.")
    };
}
