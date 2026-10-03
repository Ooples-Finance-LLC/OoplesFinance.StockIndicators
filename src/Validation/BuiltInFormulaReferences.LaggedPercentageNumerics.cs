using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedLaggedPercentage(IReadOnlyList<Bar> bars, int length)
        => bars.Select((b, i) => i < length || bars[i - length].Close == 0 ? 0
            : (new ReferenceFraction(100) * (ReferenceFraction.FromDouble(b.Close) /
                ReferenceFraction.FromDouble(bars[i - length].Close) - new ReferenceFraction(1))).ToDouble()).ToArray();
}
