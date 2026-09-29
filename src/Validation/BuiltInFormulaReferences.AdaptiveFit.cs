using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) AdaptiveFitValues(IReadOnlyList<Bar> bars, int length, double smooth = 1.5)
    {
        if (double.IsNaN(smooth) || double.IsInfinity(smooth)) throw new ArgumentOutOfRangeException(nameof(smooth));
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var ranges = new ReferenceFraction[bars.Count]; var output = new double[bars.Count]; var signals = new Signal[bars.Count]; var ageMean = R(0); var priceMean = R(0); var variance = R(0); var covariance = R(0); var before = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var price = R(bars[i].Close); var previousPrice = i == 0 ? price : R(bars[i - 1].Close); var high = R(bars[i].High); var low = R(bars[i].Low);
            var candidates = new[] { high - low, (high - previousPrice).Abs(), (low - previousPrice).Abs() }; var range = candidates.Aggregate((a, b) => a.CompareTo(b) > 0 ? a : b); ranges[i] = range; var maximum = range;
            for (var j = Math.Max(0, i - length + 1); j < i; j++) if (ranges[j].CompareTo(maximum) > 0) maximum = ranges[j];
            var gain = maximum.Sign == 0 ? .01 : Math.Max(.01, Math.Min(.99, Math.Pow((range / maximum).ToDouble(), smooth))); var weight = R(gain); var retained = R(1 - gain);
            ReferenceFraction estimate;
            if (i == 0) { priceMean = price; estimate = price; }
            else
            {
                // Merge the retained centered distribution at age-1 with a new point at age zero.
                var shiftedAge = ageMean - R(1); var priceDelta = price - priceMean;
                var mergedVariance = retained * variance + retained * weight * shiftedAge * shiftedAge;
                var mergedCovariance = retained * covariance - retained * weight * shiftedAge * priceDelta;
                variance = RoundRocBankStage(mergedVariance); covariance = RoundRocBankStage(mergedCovariance);
                ageMean = RoundRocBankStage(retained * shiftedAge); priceMean = RoundRocBankStage(priceMean + weight * priceDelta);
                estimate = RoundRocBankStage(priceMean - (variance.Sign == 0 ? R(0) : ageMean * covariance / variance));
            }
            output[i] = estimate.ToDouble(); var difference = price - estimate; var direction = difference.CompareTo(before);
            signals[i] = difference.Sign > 0 && direction > 0 ? Signal.StrongBuy : difference.Sign < 0 && direction < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None; before = difference;
        }
        return (new Dictionary<string, double[]> { { "Als", output } }, signals);
    }
}
