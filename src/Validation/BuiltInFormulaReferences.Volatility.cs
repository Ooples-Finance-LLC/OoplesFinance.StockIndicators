using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? Volatility(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        if (name == IndicatorName.ClosedFormDistanceVolatility)
        {
            var period = Integer(indicator.CreateOptions(), "Length", 14);
            return new("Cfdv", new[] { "Cfdv" }, bars => Outputs(("Cfdv", bars.Select((_, i) =>
            {
                var window = Window(bars, i, period).ToArray();
                var high = window.Sum(b => b.High);
                var low = window.Sum(b => b.Low);
                if (high == low || high + low == 0) return 0d; // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                if (low == 0 && high > 0) return 1d;
                // Ratio form of 1 - fourth-root(H*L) / sqrt((H+L)/2).
                var ratio = low / high;
                return Math.Sqrt(Math.Max(0, 1 - Math.Sqrt(2 * Math.Sqrt(ratio) / (1 + ratio))));
            }).ToArray())));
        }
        if (name == IndicatorName.MotionSmoothnessIndex)
        {
            var period = Integer(indicator.CreateOptions(), "Length", 50);
            return new("Msi", new[] { "Msi" }, bars =>
            {
                var prices = Closes(bars);
                var changes = prices.Select((v, i) => i == 0 ? 0 : v - prices[i - 1]).ToArray();
                var levelVariance = PopulationVariance(prices, period);
                var changeVariance = PopulationVariance(changes, period);
                return Outputs(("Msi", levelVariance.Select((v, i) => i + 1 < period || v == 0 ? 0 : Math.Sqrt(changeVariance[i] / v)).ToArray()));
            });
        }
        if (name == IndicatorName.QmaSmaDifference)
        {
            var period = Integer(indicator.CreateOptions(), "Length", 14);
            return new("QmaSmaDiff", new[] { "QmaSmaDiff" }, bars => Outputs(("QmaSmaDiff", bars.Select((_, i) =>
            {
                var values = Window(bars, i, period).Select(b => b.Close).ToArray();
                var scale = values.Max(v => Math.Abs(v));
                var rms = scale == 0 ? 0 : scale * Math.Sqrt(values.Average(v => Math.Pow(v / scale, 2)));
                // The quadratic mean uses available samples; the library's SMA
                // publishes zero until its full window is available.
                return rms - (i + 1 < period ? 0 : values.Average());
            }).ToArray())));
        }
        var key = name switch
        {
            IndicatorName.HistoricalVolatility => "Hv",
            IndicatorName.ParkinsonVolatility => "Pv",
            IndicatorName.GarmanKlassVolatility => "Gcv",
            IndicatorName.RogersSatchellVolatility => "Rsv",
            IndicatorName.YangZhangVolatility => "Yzv",
            _ => null
        };
        if (key is null) return null;
        var options = indicator.CreateOptions();
        var length = Math.Max(name == IndicatorName.YangZhangVolatility ? 2 : 1, Integer(options, "Length"));
        var kind = AverageKind(options, 2);
        if (name == IndicatorName.GarmanKlassVolatility && kind == 0) return null;
        return new(key, name == IndicatorName.GarmanKlassVolatility ? new[] { key, "Signal" } : new[] { key }, bars =>
        {
            // Independent OHLC estimators. HV is percent, annualized on 365 days;
            // the range estimators are fractional volatility annualized on 252 days.
            var overnight = bars.Select((b, i) => i == 0 ? 0 : LogRatio(b.Open, bars[i - 1].Close)).ToArray();
            var returns = bars.Select((b, i) => i == 0 ? 0 : name == IndicatorName.HistoricalVolatility
                ? ReferenceSameSignLogRatio(b.Close, bars[i - 1].Close) : LogRatio(b.Close, bars[i - 1].Close)).ToArray();
            var intraday = bars.Select(b => LogRatio(b.Close, b.Open)).ToArray();
            var ranges = bars.Select(b => Math.Pow(LogRatio(b.High, b.Low), 2)).ToArray();
            var rs = bars.Select(b => LogRatio(b.High, b.Close) * LogRatio(b.High, b.Open)
                + LogRatio(b.Low, b.Close) * LogRatio(b.Low, b.Open)).ToArray();
            var line = new double[bars.Count];
            for (var i = 0; i < line.Length; i++)
            {
                var needsPrevious = name == IndicatorName.HistoricalVolatility || name == IndicatorName.YangZhangVolatility;
                if (i < length - (needsPrevious ? 0 : 1)) continue;
                double variance;
                switch (name)
                {
                    case IndicatorName.HistoricalVolatility:
                        variance = CenteredVariance(Window(returns, i, length).ToArray(), false);
                        line[i] = 100 * Math.Sqrt(365 * variance);
                        continue;
                    case IndicatorName.ParkinsonVolatility:
                        variance = Window(ranges, i, length).Average() / (4 * Math.Log(2));
                        break;
                    case IndicatorName.GarmanKlassVolatility:
                        variance = Enumerable.Range(i - length + 1, length)
                            .Average(j => ranges[j] / 2 - (2 * Math.Log(2) - 1) * intraday[j] * intraday[j]);
                        break;
                    case IndicatorName.RogersSatchellVolatility:
                        variance = Window(rs, i, length).Average();
                        break;
                    default:
                        var weight = .34 / (1.34 + (length + 1d) / (length - 1));
                        // A zero denominator omits the residual, retaining the full-window divisors.
                        double MaskedVariance(double[] values, Func<int, bool> included)
                        {
                            var positions = Enumerable.Range(i - length + 1, length).Where(included).ToArray();
                            var mean = positions.Sum(j => values[j]) / length;
                            return positions.Sum(j => Math.Pow(values[j] - mean, 2)) / (length - 1);
                        }
                        variance = MaskedVariance(overnight, j => j > 0 && bars[j - 1].Close != 0)
                            + weight * MaskedVariance(intraday, j => bars[j].Open != 0)
                            + (1 - weight) * Window(rs, i, length).Average();
                        break;
                }
                line[i] = Math.Sqrt(252 * Math.Max(0, variance));
            }
            return name == IndicatorName.GarmanKlassVolatility
                ? Outputs((key, line), ("Signal", Average(line, 7, kind))) : Outputs((key, line));
        });
    }

    private static double LogRatio(double numerator, double denominator) =>
        ReferenceSameSignLogRatio(numerator, denominator);

    private static double CenteredVariance(double[] values, bool sample)
    {
        var mean = values[0] + values.Average(v => v - values[0]);
        return values.Sum(v => (v - mean) * (v - mean)) / (values.Length - (sample ? 1 : 0));
    }
}
