using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget LogisticCorrelationBudget = new(0, 4e-15, requireSameSign: true);
    internal static IReadOnlyDictionary<string, double[]> RankLogisticOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var logistic = indicator.BatchName == IndicatorName.LogisticCorrelation;
        var options = indicator.CreateOptions(); var period = Integer(options, "Length", logistic ? 100 : 20);
        var gain = logistic ? Number(options, 10, "K") : 0;
        var result = new double[bars.Count]; var fits = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Min(i + 1, period); var n = new ReferenceFraction(count);
            var prices = bars.Skip(i - count + 1).Take(count).Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
            var mean = prices.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / n;
            var center = new ReferenceFraction(count - 1) / new ReferenceFraction(2);
            var xy = new ReferenceFraction(0); var xx = new ReferenceFraction(0); var yy = new ReferenceFraction(0);
            for (var j = 0; j < count; j++)
            {
                var x = new ReferenceFraction(j) - center; var y = prices[j] - mean;
                xy += x * y; xx += x * x; yy += y * y;
            }
            if (logistic)
            {
                var correlation = xx.Sign == 0 || yy.Sign == 0 ? 0 : xy.Sign * (xy * xy / (xx * yy)).SqrtToDouble();
                var exponent = Math.Min(100, -gain * correlation);
                result[i] = (ReferenceFraction.FromDouble(-exponent) / new ReferenceFraction(2)).LogisticPercentToDouble() / 100;
            }
            else
            {
                fits[i] = RoundStrengthStage(count == 1 ? mean : mean + xy / xx * center);
                // Sorting price makes concordance depend only on fitted-value order.
                // Ties contribute zero; missing history is padded with zero pairs.
                var pairs = Enumerable.Range(i - period + 1, period).Select(j =>
                    (Price: j < 0 ? 0 : bars[j].Close, Fit: j < 0 ? new ReferenceFraction(0) : fits[j])).OrderBy(v => v.Price).ToArray();
                long score = 0;
                for (var j = 0; j < pairs.Length; j++)
                    foreach (var other in pairs.Skip(j + 1))
                        if (other.Price != pairs[j].Price) score += other.Fit.CompareTo(pairs[j].Fit);
                result[i] = period == 1 ? 0 : (new ReferenceFraction(2 * score) / (new ReferenceFraction(period) * new ReferenceFraction(period - 1))).ToDouble();
            }
        }
        return Outputs((logistic ? "LogCorr" : "Krcc", result));
    }
}
