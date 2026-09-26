using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? RemainingTrends(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName is not (IndicatorName.ModifiedGannHiloActivator or IndicatorName.RobustWeightingOscillator or IndicatorName.R2AdaptiveRegression or IndicatorName.RecursiveRelativeStrengthIndex or IndicatorName.PercentageTrend)) return null;
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length");
        var kind = AverageKind(options, 1);
        if (kind == 0) return null;
        switch (indicator.BatchName)
        {
            case IndicatorName.ModifiedGannHiloActivator:
                return new("Ghla", new[] { "Ghla" }, bars =>
                {
                    // With the exposed multiplier of one, the envelopes are simply
                    // smoothed rolling candle extrema. Reconstruct the latest switching
                    // event instead of sharing the production direction recurrence.
                    var upper = Average(bars.Select((_, i) => bars.Skip(Math.Max(0, i-length+1)).Take(Math.Min(length, i+1)).Max(b => b.High)).ToArray(), length, kind);
                    var lower = Average(bars.Select((_, i) => bars.Skip(Math.Max(0, i-length+1)).Take(Math.Min(length, i+1)).Min(b => b.Low)).ToArray(), length, kind);
                    var events = Enumerable.Range(0, bars.Count).Where(i => bars[i].Close > upper[i] || bars[i].Close > lower[i]).ToArray();
                    return Outputs(("Ghla", Enumerable.Range(0, bars.Count).Select(i =>
                    {
                        var last = events.Where(j => j <= i).DefaultIfEmpty(-1).Last();
                        return last >= 0 && bars[last].Close > upper[last] ? lower[i] : upper[i];
                    }).ToArray()));
                });
            case IndicatorName.PercentageTrend:
                return new("Pti", new[] { "Pti" }, bars =>
                {
                    var prices = Closes(bars); var percentage = Number(options, .15, "Pct");
                    double At(int i) => i < 0 ? 0 : prices[i];
                    return Outputs(("Pti", prices.Select((price, i) =>
                    {
                        var line = price; var span = 0;
                        for (var lag = 1; lag <= length; lag++)
                        {
                            var older = At(i-lag); var newer = At(i-lag+1);
                            if (newer <= line && older > line || newer >= line && older < line) span = 0;
                            // The retained span always samples the suffix ending at the
                            // current bar; include the inspected historical observation.
                            var candidates = Enumerable.Range(i-span, span+1).Select(At).Append(older);
                            line = older > line ? candidates.Max()*(1-percentage) : candidates.Min()*(1+percentage);
                            span++;
                        }
                        return line;
                    }).ToArray()));
                });
            case IndicatorName.RecursiveRelativeStrengthIndex:
                return new("Rrsi", new[] { "Rrsi" }, bars =>
                {
                    var prices = Closes(bars);
                    var source = Average(prices.Select((v, i) => i < length ? 0 : v-prices[i-length]).ToArray(), length, kind);
                    var strength = MotionRsi(source, length);
                    var output = new double[bars.Count]; var midpoint = new double[bars.Count];
                    var rising = new bool[bars.Count];
                    for (var i = 0; i < bars.Count; i++)
                    {
                        // Only the j=length endpoint survives the legacy inner loop.
                        // At that endpoint k=1, so gain/loss smoothing collapses to a
                        // binary nondecreasing test, followed by a delayed moving count.
                        midpoint[i] = (strength[i]+(i < length ? source[i] : output[i-length]))/2;
                        rising[i] = midpoint[i] >= (i < length ? 0 : midpoint[i-length]);
                        output[i] = i < length ? (rising[i] ? 100 : 0)
                            : 100d*Enumerable.Range(i-length, length).Count(j => rising[j])/length;
                    }
                    return Outputs(("Rrsi", output));
                });
            case IndicatorName.R2AdaptiveRegression:
                return new("R2ar", new[] { "R2ar" }, bars =>
                {
                    var prices = Closes(bars);
                    var linear = RegressionEndpoints(prices, length);
                    var mean = Average(prices, length, kind);
                    var variance = PopulationVariance(prices, length);
                    var lagged = new double[bars.Count]; var errors = new double[bars.Count];
                    var adaptive = new double[bars.Count]; var result = new double[bars.Count];
                    double Correlation(double[] source, int i)
                    {
                        var first = Math.Max(0, i-length+1);
                        var x = source.Skip(first).Take(i-first+1).ToArray();
                        var y = prices.Skip(first).Take(i-first+1).ToArray();
                        var xMean = x.Average(); var yMean = y.Average();
                        var norm = x.Sum(v => (v-xMean)*(v-xMean))*y.Sum(v => (v-yMean)*(v-yMean));
                        return norm == 0 ? 0 : x.Select((v, j) => (v-xMean)*(y[j]-yMean)).Sum()/Math.Sqrt(norm);
                    }
                    for (var i = 0; i < bars.Count; i++)
                    {
                        lagged[i] = i == 0 ? prices[i] : result[i-1];
                        var center = Window(lagged, i, length).Average();
                        errors[i] = Math.Pow(lagged[i]-center, 2);
                        var energy = Window(errors, i, length).Average();
                        var gain = energy == 0 ? 0 : Math.Sqrt(variance[i]/energy)*Correlation(lagged, i);
                        adaptive[i] = mean[i]+gain*(lagged[i]-center);
                        var linearWeight = Math.Pow(Correlation(linear, i), 2);
                        var adaptiveWeight = Math.Pow(Correlation(adaptive, i), 2);
                        result[i] = lagged[i]+linearWeight*(linear[i]-lagged[i])+adaptiveWeight*(adaptive[i]-lagged[i]);
                    }
                    return Outputs(("R2ar", result));
                });
            case IndicatorName.RobustWeightingOscillator:
                return new("Rwo", new[] { "Rwo" }, bars =>
                {
                    var prices = Closes(bars);
                    var mean = Average(prices, length, kind);
                    var timeMean = Average(Enumerable.Range(0, bars.Count).Select(i => (double)i).ToArray(), length, kind);
                    var residual = prices.Select((value, i) =>
                    {
                        // Pairwise differences give the least-squares slope without
                        // correlation, standard-deviation engines, or raw moments.
                        var first = Math.Max(0, i-length+1);
                        double numerator = 0, denominator = 0;
                        for (var a = first; a <= i; a++)
                            for (var b = a+1; b <= i; b++)
                            {
                                numerator += (b-a)*(prices[b]-prices[a]);
                                denominator += (b-a)*(double)(b-a);
                            }
                        var slope = i+1 < length || denominator == 0 ? 0 : numerator/denominator;
                        var intercept = mean[i]-slope*timeMean[i];
                        // The named oscillator's legacy transform is x-a-b*x;
                        // it is not the residual x-(a*time+b).
                        return value*(1-intercept)-slope;
                    }).ToArray();
                    return Outputs(("Rwo", Average(residual, length, kind)));
                });
            default: return null;
        }
    }
}
