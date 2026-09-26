using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedVolumeMomentumOscillator(IReadOnlyList<Bar> bars, object options)
    {
        var projected = bars.Select(b => new Bar(b.Time, b.Volume, b.Volume, b.Volume, b.Volume, b.Volume)).ToArray();
        return RoundedNormalizedMacd(projected, new NormalizedMacdSpecOptions(Integer(options, "ShortLength", 5), Integer(options, "LongLength", 20)));
    }
}
