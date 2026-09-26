using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EaseOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var divisor = ReferenceFraction.FromDouble(Number(indicator.CreateOptions(), 1000000, "Divisor"));
        var output = bars.Select((b, i) =>
        {
            if (i == 0 || b.High == b.Low || b.Volume == 0) return 0d;
            var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low);
            var movement = (high + low - ReferenceFraction.FromDouble(bars[i - 1].High) - ReferenceFraction.FromDouble(bars[i - 1].Low)) / new ReferenceFraction(2);
            return (divisor * movement * (high - low) / ReferenceFraction.FromDouble(b.Volume)).ToDouble();
        }).ToArray();
        return Outputs(("Eom", output));
    }
}
