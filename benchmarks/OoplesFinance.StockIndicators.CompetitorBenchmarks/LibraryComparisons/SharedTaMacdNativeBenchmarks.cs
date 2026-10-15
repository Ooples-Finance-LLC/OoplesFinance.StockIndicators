using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTaMacdNativeBenchmarks : SharedTaPackedBenchmarkBase
{
    [Params("TaLib.Functions.Macd", "TaLib.Functions.MacdFix", "TaLib.Functions.MacdExt")]
    public string Operation { get; set; } = "";
    protected override string Mapping => Operation;
    protected override int Lookback => 33;
    protected override int Columns => 3;
    protected override IIndicator Create() => Operation switch
    {
        "TaLib.Functions.Macd" => new AlignedEmaMacd(),
        "TaLib.Functions.MacdFix" => new AlignedEmaMacd(fixedCoefficients: true),
        "TaLib.Functions.MacdExt" => new ClassicMacd(),
        _ => throw new ArgumentException("Unknown TA MACD operation.")
    };

    protected override TALib.Core.RetCode Call(System.Range input, double[][] output, out System.Range range) => Operation switch
    {
        "TaLib.Functions.Macd" => Functions.Macd<double>(Data.Closes, input,
            output[0], output[1], output[2], out range, 12, 26, 9),
        "TaLib.Functions.MacdFix" => Functions.MacdFix<double>(Data.Closes, input,
            output[0], output[1], output[2], out range, 9),
        "TaLib.Functions.MacdExt" => Functions.MacdExt<double>(Data.Closes, input,
            output[0], output[1], output[2], out range,
            12, TALib.Core.MAType.Sma, 26, TALib.Core.MAType.Sma, 9, TALib.Core.MAType.Sma),
        _ => throw new ArgumentException("Unknown TA MACD operation.")
    };
}
