using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static IndicatorValidationRule? OvershootTrajectory(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 14);
        var kind = AverageKind(options, 1);
        if (kind == 0) return null;
        return FullReference(0, bars =>
        {
            var prices = Closes(bars);
            var meanPrice = Average(prices, length, kind);
            var meanIndex = Average(Enumerable.Range(0, prices.Length).Select(i => (double)i).ToArray(), length, kind);
            // Recompute each window and feed back only this reference's own predictions.
            var predicted = new double[prices.Length];
            var errors = new double[prices.Length];
            var errorMeans = new double[prices.Length];
            for (var i = 0; i < prices.Length; i++)
            {
                var previous = i == 0 ? 0 : predicted[i - 1] != 0 ? predicted[i - 1] : prices[i - 1];
                errors[i] = Math.Abs(prices[i] - previous);
                errorMeans[i] = Window(errors, i, (int)Math.Ceiling(length / 2d)).Average();
                if (i + 1 < length || length == 1) { predicted[i] = meanPrice[i]; continue; }
                var values = Window(prices, i, length).ToArray();
                var center = (length - 1d) / 2;
                var mean = values.Average();
                var scatter = Enumerable.Range(0, length).Sum(j => Math.Pow(j - center, 2));
                var slope = values.Select((v, j) => (v - mean) * (j - center)).Sum() / scatter;
                var maximum = Window(errorMeans, i, length).Max();
                predicted[i] = meanPrice[i] + (maximum == 0 ? 0 : slope * (i - meanIndex[i]) * errorMeans[i] / maximum);
            }
            return predicted;
        });
    }
}
