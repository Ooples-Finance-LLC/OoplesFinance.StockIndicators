using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTaWindowNativeBenchmarks : SharedTaPackedBenchmarkBase
{
    private const int Period = 20;
    [Params("TaLib.Functions.AvgDev", "TaLib.Functions.Accbands")]
    public string Operation { get; set; } = "";
    protected override string Mapping => Operation;
    protected override int Lookback => Period - 1;
    protected override int Columns => Operation == "TaLib.Functions.Accbands" ? 3 : 1;
    protected override bool IndependentWindows => Operation == "TaLib.Functions.AvgDev";

    protected override IIndicator Create() => Operation switch
    {
        "TaLib.Functions.AvgDev" => new WindowMeanAbsoluteDeviation(Period),
        "TaLib.Functions.Accbands" => new RangeAccelerationBands(Period),
        _ => throw new ArgumentException("Unknown TA window operation.")
    };

    protected override TALib.Core.RetCode Call(System.Range input, double[][] output, out System.Range range) =>
        Operation == "TaLib.Functions.Accbands"
            ? Functions.Accbands<double>(Data.Highs, Data.Lows, Data.Closes, input,
                output[0], output[1], output[2], out range, Period)
            : Functions.AvgDev<double>(Data.Closes, input, output[0], out range, Period);
}
