using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedVolumeZone(IReadOnlyList<Bar> bars, int length)
    {
        var signed = bars.Select((b, i) => i == 0 ? 0 : b.Close > bars[i - 1].Close ? b.Volume : -b.Volume).ToArray();
        var numerator = RoundedEma(signed, length);
        var denominator = RoundedEma(bars.Select(b => b.Volume).ToArray(), length);
        return numerator.Select((v, i) => denominator[i] == 0 ? 0
            : (new ReferenceFraction(100) * ReferenceFraction.FromDouble(v) / ReferenceFraction.FromDouble(denominator[i])).ToDouble()).ToArray();
    }
}
