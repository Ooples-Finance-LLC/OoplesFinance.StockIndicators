using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] HerrickPayoffValues(IReadOnlyList<Bar> bars, double pointValue, bool selected = false)
    {
        var result = new double[bars.Count];
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = bars.Select(b => R(selected ? b.Close : ExactPriceMean(b.High, b.Low))).ToArray();
        for (var i = 1; i < bars.Count; i++)
        {
            var change = prices[i] - prices[i - 1];
            var opening = R(Math.Min(bars[i].Open, bars[i - 1].Open));
            var closeChange = (R(bars[i].Close) - R(bars[i - 1].Close)).Abs();
            var numerator = opening.Sign == 0 ? new ReferenceFraction(1)
                : new ReferenceFraction(2) * opening + (change.Sign < 0 ? new ReferenceFraction(0) - closeChange : closeChange);
            var denominator = opening.Sign == 0 ? new ReferenceFraction(1) : new ReferenceFraction(2) * opening;
            result[i] = (change * R(pointValue) * R(bars[i].Volume) * numerator / denominator).ToDouble();
        }
        return result;
    }
}
