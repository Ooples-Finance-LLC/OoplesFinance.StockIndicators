using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] AdaptiveGravityValues(IReadOnlyList<Bar> bars, int length)
    {
        var periods = AdaptiveCyberValues(bars, length, .07)["Period"]; var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = (int)Math.Ceiling(periods[i] / 2); var center = new ReferenceFraction((window + 1) / 2); var mass = new ReferenceFraction(0); var moment = new ReferenceFraction(0);
            for (var j = Math.Max(0, i - window + 1); j <= i; j++) { var price = ReferenceFraction.FromDouble(bars[j].Close); mass += price; moment += new ReferenceFraction(i - j + 1) * price; }
            result[i] = mass.Sign == 0 ? 0 : (center - moment / mass).ToDouble();
        }
        return result;
    }
}
