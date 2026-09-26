using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? KaufmanRegressionFormula(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName is not (IndicatorName.KaufmanAdaptiveCorrelationOscillator
            or IndicatorName.KaufmanAdaptiveLeastSquaresMovingAverage)) return null;
        var length = Integer(indicator.CreateOptions(), "Length");
        var regression = indicator.BatchName == IndicatorName.KaufmanAdaptiveLeastSquaresMovingAverage;
        return new(regression ? "Kalsma" : "Kaco", regression ? new[] { "Kalsma" }
            : new[] { "IndexSt", "SrcSt", "Kaco" }, bars =>
        {
            var prices = Closes(bars);
            var weights = new decimal[bars.Count];
            var timeDeviation = new double[bars.Count]; var priceDeviation = new double[bars.Count];
            var correlation = new double[bars.Count]; var fitted = new double[bars.Count];
            for (var i = 0; i < bars.Count; i++)
            {
                double travel = 0;
                if (i >= length)
                    for (var j = i-length+1; j <= i; j++) travel += Math.Abs(prices[j]-prices[j-1]);
                var efficiency = travel == 0 ? 0 : Math.Abs(prices[i]-prices[i-length])/travel;
                var gain = i < length ? 1m : (decimal)Math.Pow(2d/31 + efficiency*(2d/3-2d/31), 2);
                for (var j = 0; j < i; j++) weights[j] *= 1-gain;
                weights[i] = gain;
                // Expand every surviving historical weight, then directly evaluate
                // the weighted least-squares objective. No production moment recurrence.
                decimal total = 0, meanTime = 0, meanPrice = 0;
                for (var j = 0; j <= i; j++)
                {
                    total += weights[j];
                    meanTime += weights[j]*(j-i);
                    meanPrice += weights[j]*(decimal)prices[j];
                }
                meanTime /= total; meanPrice /= total;
                decimal timeVariance = 0, priceVariance = 0, covariance = 0;
                for (var j = 0; j <= i; j++)
                {
                    var dx = j-i-meanTime;
                    var dy = (decimal)prices[j]-meanPrice;
                    timeVariance += weights[j]*dx*dx;
                    priceVariance += weights[j]*dy*dy;
                    covariance += weights[j]*dx*dy;
                }
                timeDeviation[i] = Math.Sqrt((double)(timeVariance/total));
                priceDeviation[i] = Math.Sqrt((double)(priceVariance/total));
                correlation[i] = timeVariance == 0 || priceVariance == 0 ? 0
                    : (double)(covariance/total)/timeDeviation[i]/priceDeviation[i];
                fitted[i] = (double)(timeVariance == 0 ? meanPrice : meanPrice-meanTime*covariance/timeVariance);
            }
            return regression ? Outputs(("Kalsma", fitted))
                : Outputs(("IndexSt", timeDeviation), ("SrcSt", priceDeviation), ("Kaco", correlation));
        });
    }
}
