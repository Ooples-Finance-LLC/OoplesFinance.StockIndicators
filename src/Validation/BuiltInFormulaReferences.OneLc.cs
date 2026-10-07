using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) OneLcValues(IReadOnlyList<Bar> bars, int length, int kind = 1, IReadOnlyList<double>? externalAverage = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = bars.Select(b => R(b.Close)).ToArray();
        var means = externalAverage is not null ? externalAverage.Select(R).ToArray() : kind is 1 or 2 or 3 or 6 ? SmoothStrengthStage(prices, length, kind) : Average(Closes(bars), length, kind).Select(R).ToArray();
        var values = new double[bars.Count]; var signals = new Signal[bars.Count]; var before = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var correction = R(0);
            if (length > 1 && i + 1 >= length)
            {
                // Direct centered dot product, independent of the rolling integer moments.
                var covariance = R(0); var center = R(length - 1) / R(2);
                for (var j = 0; j < length; j++) covariance += (R(j) - center) * prices[i - length + 1 + j];
                covariance /= R(length); var timeVariance = (R(length) * R(length) - R(1)) / R(12);
                var squared = covariance * covariance * R(1.7) * R(1.7) / timeVariance; var scale = R(1);
                while (true)
                {
                    var magnitude = (squared / (scale * scale)).SqrtToDouble();
                    if (!double.IsInfinity(magnitude)) { correction = R(covariance.Sign * magnitude) * scale; break; }
                    scale *= R(4294967296d);
                }
            }
            var estimate = RoundRocBankStage(means[i] + correction); values[i] = estimate.ToDouble(); var difference = prices[i] - estimate;
            signals[i] = difference.Sign > 0 && difference.CompareTo(before) > 0 ? Signal.StrongBuy : difference.Sign < 0 && difference.CompareTo(before) < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None; before = difference;
        }
        return (new Dictionary<string, double[]> { { "1lsma", values } }, signals);
    }
}
