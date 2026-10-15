using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTaHilbertNativeBenchmarks : SharedTaPackedBenchmarkBase
{
    [Params("TaLib.Functions.HtDcPhase", "TaLib.Functions.HtSine", "TaLib.Functions.HtTrendline")]
    public string Operation { get; set; } = "";
    protected override string Mapping => Operation;
    protected override int Lookback => 63;
    protected override int Columns => Operation == "TaLib.Functions.HtSine" ? 2 : 1;
    protected override IIndicator Create() => Operation switch
    {
        "TaLib.Functions.HtDcPhase" => new DelayedHilbertPhase(),
        "TaLib.Functions.HtSine" => new DelayedHilbertSine(),
        "TaLib.Functions.HtTrendline" => new DelayedHilbertTrendline(),
        _ => throw new ArgumentException("Unknown TA Hilbert operation.")
    };
    protected override TALib.Core.RetCode Call(System.Range input, double[][] output, out System.Range range) => Operation switch
    {
        "TaLib.Functions.HtDcPhase" => Functions.HtDcPhase<double>(Data.Closes, input, output[0], out range),
        "TaLib.Functions.HtSine" => Functions.HtSine<double>(Data.Closes, input, output[0], output[1], out range),
        "TaLib.Functions.HtTrendline" => Functions.HtTrendline<double>(Data.Closes, input, output[0], out range),
        _ => throw new ArgumentException("Unknown TA Hilbert operation.")
    };
}

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTaHilbertTrendModeBenchmarks : SharedTaPackedBenchmarkBase<int>
{
    [Params("TaLib.Functions.HtTrendMode")]
    public string Operation { get; set; } = "";
    protected override string Mapping => Operation;
    protected override int Lookback => 63;
    protected override int Columns => 1;
    protected override IIndicator Create() => new DelayedHilbertTrendMode();
    protected override TALib.Core.RetCode Call(System.Range input, int[][] output, out System.Range range) =>
        Functions.HtTrendMode<double>(Data.Closes, input, output[0], out range);
}
