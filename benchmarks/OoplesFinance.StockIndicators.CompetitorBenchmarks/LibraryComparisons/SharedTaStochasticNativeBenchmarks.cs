using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTaStochasticNativeBenchmarks : SharedTaPackedBenchmarkBase
{
    [Params("TaLib.Functions.Stoch", "TaLib.Functions.StochF", "TaLib.Functions.StochRsi")]
    public string Operation { get; set; } = "";
    protected override string Mapping => Operation;
    protected override int Lookback => RequiredLookback(Operation);
    protected override int Columns => 2;
    internal static int RequiredLookback(string operation) => operation switch
    {
        "TaLib.Functions.Stoch" => 8,
        "TaLib.Functions.StochF" => 6,
        "TaLib.Functions.StochRsi" => 20,
        _ => throw new ArgumentException("Unknown TA stochastic operation.")
    };
    protected override IIndicator Create() => Operation switch
    {
        "TaLib.Functions.Stoch" => new ClassicStochastic(),
        "TaLib.Functions.StochF" => new ClassicStochastic(kPeriod: 1),
        "TaLib.Functions.StochRsi" => new ClassicStochasticRsi(),
        _ => throw new ArgumentException("Unknown TA stochastic operation.")
    };
    protected override TALib.Core.RetCode Call(System.Range input, double[][] output, out System.Range range) => Operation switch
    {
        "TaLib.Functions.Stoch" => Functions.Stoch<double>(Data.Highs, Data.Lows, Data.Closes, input,
            output[0], output[1], out range, 5, 3, TALib.Core.MAType.Sma, 3, TALib.Core.MAType.Sma),
        "TaLib.Functions.StochF" => Functions.StochF<double>(Data.Highs, Data.Lows, Data.Closes, input,
            output[0], output[1], out range, 5, 3, TALib.Core.MAType.Sma),
        "TaLib.Functions.StochRsi" => Functions.StochRsi<double>(Data.Closes, input,
            output[0], output[1], out range, 14, 5, 3, TALib.Core.MAType.Sma),
        _ => throw new ArgumentException("Unknown TA stochastic operation.")
    };
}
