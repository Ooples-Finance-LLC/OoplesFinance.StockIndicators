using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTaMultiBarNativeBenchmarks : SharedTaCandleBenchmarkBase
{
    [Params("Breakaway", "ConcealingBabySwallow", "LadderBottom", "MatHold", "RisingFallingThreeMethods", "Hikkake", "HikkakeModified", "TasukiGap", "Tristar", "UpDownSideGapThreeMethods")]
    public override string Operation { get; set; } = "";
    protected override int Lookback => RequiredLookback(Operation);
    internal static int RequiredLookback(string operation) => operation switch
    {
        "UpDownSideGapThreeMethods" => 2,
        "Hikkake" => 5,
        "TasukiGap" => 7,
        "HikkakeModified" => 10,
        "Tristar" => 12,
        "ConcealingBabySwallow" => 13,
        _ => 14
    };

    protected override int NativeLookback() => Operation switch
    {
        "Breakaway" => Candles.BreakawayLookback(),
        "ConcealingBabySwallow" => Candles.ConcealingBabySwallowLookback(),
        "LadderBottom" => Candles.LadderBottomLookback(),
        "MatHold" => Candles.MatHoldLookback(),
        "RisingFallingThreeMethods" => Candles.RisingFallingThreeMethodsLookback(),
        "Hikkake" => Candles.HikkakeLookback(),
        "HikkakeModified" => Candles.HikkakeModifiedLookback(),
        "TasukiGap" => Candles.TasukiGapLookback(),
        "Tristar" => Candles.TristarLookback(),
        "UpDownSideGapThreeMethods" => Candles.UpDownSideGapThreeMethodsLookback(),
        _ => throw new ArgumentException("Unknown TA multi-bar operation.")
    };

    protected override IIndicator Create() => Operation switch
    {
        "MatHold" or "RisingFallingThreeMethods" => FiveCandleContinuationComparison.Create(Operation),
        "Breakaway" or "ConcealingBabySwallow" or "LadderBottom" => ExtendedReversalComparison.Create(Operation),
        "Hikkake" or "HikkakeModified" => HikkakeComparison.Create(Operation),
        _ => GapContinuationComparison.Create(Operation, Operation == "TasukiGap" ? 5 : 10)
    };

    protected override TALib.Core.RetCode ComputeNative(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> packed, out System.Range range) => Operation switch
    {
        "Breakaway" => Candles.Breakaway<double>(open, high, low, close, System.Range.All, packed, out range),
        "ConcealingBabySwallow" => Candles.ConcealingBabySwallow<double>(open, high, low, close, System.Range.All, packed, out range),
        "LadderBottom" => Candles.LadderBottom<double>(open, high, low, close, System.Range.All, packed, out range),
        "MatHold" => Candles.MatHold<double>(open, high, low, close, System.Range.All, packed, out range, .5),
        "RisingFallingThreeMethods" => Candles.RisingFallingThreeMethods<double>(open, high, low, close, System.Range.All, packed, out range),
        "Hikkake" => Candles.Hikkake<double>(open, high, low, close, System.Range.All, packed, out range),
        "HikkakeModified" => Candles.HikkakeModified<double>(open, high, low, close, System.Range.All, packed, out range),
        "TasukiGap" => Candles.TasukiGap<double>(open, high, low, close, System.Range.All, packed, out range),
        "Tristar" => Candles.Tristar<double>(open, high, low, close, System.Range.All, packed, out range),
        _ => Candles.UpDownSideGapThreeMethods<double>(open, high, low, close, System.Range.All, packed, out range)
    };
}
