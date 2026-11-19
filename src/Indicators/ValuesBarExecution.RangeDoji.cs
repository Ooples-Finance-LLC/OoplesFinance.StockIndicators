#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // The original state needs the prior P ranges and its startup count. Dragonfly
    // has one additional startup bar. The shared scheduler seeds window-1 bars.
    private static int RangeDojiWindow(IIndicator indicator) =>
        (indicator is AverageRangeDoji or AverageRangeDragonflyDoji or AverageRangeGravestoneDoji
            or AverageRangeLongLeggedDoji or TakuriLineCandle) && indicator.WarmupBars < int.MaxValue
            ? indicator.WarmupBars + 1 : 0;

    private readonly struct RangeDojiKernel(AverageRangeDojiState state) : IBoundedKernel<RangeDojiKernel>
    {
        public static RangeDojiKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new((AverageRangeDojiState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
    }
}
#endif
