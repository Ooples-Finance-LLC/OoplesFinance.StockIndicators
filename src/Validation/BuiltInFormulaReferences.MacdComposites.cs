using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? MacdComposites(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var kind = AverageKind(options, 1);
        if (kind == 0) return null;
        switch (indicator.BatchName)
        {
            case IndicatorName._4MovingAverageConvergenceDivergence:
            case IndicatorName._4PercentagePriceOscillator:
                var percentage = indicator.BatchName == IndicatorName._4PercentagePriceOscillator;
                var stem = percentage ? "Ppo" : "Macd";
                return new(stem + "1", new[] { stem + "1", "Signal1", "Histogram1", stem + "2", "Signal2", "Histogram2" }, bars =>
                {
                    var prices = Closes(bars);
                    var smoothPeriod = Integer(options, "Length1", 5);
                    double[] Pair(int numerator, int denominator)
                    {
                        var first = Average(prices, numerator, kind);
                        var second = Average(prices, denominator, kind);
                        return first.Zip(second, (a, b) => percentage ? b == 0 ? 0 : 100 * (a / b - 1) : a - b).ToArray();
                    }
                    var first = Pair(smoothPeriod, Integer(options, "Length3", 10));
                    var second = Pair(Integer(options, "Length4", 17), Integer(options, "Length2", 8));
                    var firstSignal = Average(first, smoothPeriod, kind);
                    var secondSignal = Average(second, smoothPeriod, kind);
                    // Length5/6 and blue/yellow multipliers do not affect the six published lines.
                    return Outputs((stem + "1", first), ("Signal1", firstSignal),
                        ("Histogram1", first.Zip(firstSignal, (a, b) => a - b).ToArray()),
                        (stem + "2", second), ("Signal2", secondSignal),
                        ("Histogram2", second.Zip(secondSignal, (a, b) => a - b).ToArray()));
                });
            case IndicatorName.WaddahAttarExplosion:
                return new("T1", new[] { "T1", "T2", "E1", "TrendUp", "TrendDn" }, bars =>
                {
                    var prices = Closes(bars);
                    var fast = Integer(options, "FastLength", 20);
                    var slow = Integer(options, "SlowLength", 40);
                    var scale = Number(options, 150, "Sensitivity");
                    // Linearity lets us filter price differences instead of subtracting four price filters.
                    double[] Strength(int lag)
                    {
                        var differences = prices.Select((_, i) => (i < lag ? 0 : prices[i - lag])
                            - (i <= lag ? 0 : prices[i - lag - 1])).ToArray();
                        return Average(differences, fast, 3).Zip(Average(differences, slow, 3), (a, b) => scale * (a - b)).ToArray();
                    }
                    var current = Strength(0);
                    return Outputs(("T1", current), ("T2", Strength(2)),
                        ("E1", PopulationVariance(prices, fast).Select(v => 4 * Math.Sqrt(v)).ToArray()),
                        ("TrendUp", current.Select(v => Math.Max(0, v)).ToArray()),
                        ("TrendDn", current.Select(v => Math.Max(0, -v)).ToArray()));
                });
            case IndicatorName.TrendForceHistogram:
                return new("Tfh", new[] { "Tfh" }, bars =>
                {
                    var prices = Closes(bars);
                    var period = Math.Max(2, Integer(options, "Length", 14));
                    var up = prices.Select((v, i) => v > (i == 0 ? 0 : Window(prices, i - 1, period).Max())).ToArray();
                    var down = prices.Select((v, i) => v < (i == 0 ? 0 : Window(prices, i - 1, period).Min())).ToArray();
                    int CountSinceReset(bool[] events, bool[] opposite, int end)
                    {
                        var reset = Enumerable.Range(0, end + 1).Where(j => opposite[j] && (j == 0 || !opposite[j - 1])).DefaultIfEmpty(-1).Max();
                        return Enumerable.Range(reset + 1, end - reset).Count(j => events[j]);
                    }
                    var counts = prices.Select((_, i) => (CountSinceReset(up, down, i) + CountSinceReset(down, up, i)) / 2d).ToArray();
                    return Outputs(("Tfh", counts.Select((v, i) => v - counts.Take(i + 1).Average()).ToArray()));
                });
            case IndicatorName.DiNapoliMovingAverageConvergenceDivergence:
                return new("Macd", new[] { "FastS", "SlowS", "Macd", "Signal", "Histogram" }, bars =>
                {
                    // Fractional-period zero-seeded filters expanded as observation weights.
                    double[] Filter(double[] values, double period) => values.Select((_, i) => Enumerable.Range(0, i + 1)
                        .Sum(j => values[j] * 2 / (period + 1) * Math.Pow(1 - 2 / (period + 1), i - j))).ToArray();
                    var fast = Filter(Closes(bars), Number(options, 8.3896, "Sc"));
                    var slow = Filter(Closes(bars), Number(options, 17.5185, "Lc"));
                    var line = fast.Zip(slow, (f, s) => f - s).ToArray();
                    var signal = Filter(line, Number(options, 9.0503, "Sp"));
                    return Outputs(("FastS", fast), ("SlowS", slow), ("Macd", line), ("Signal", signal),
                        ("Histogram", line.Zip(signal, (v, s) => v - s).ToArray()));
                });
            case IndicatorName.MovingAverageConvergenceDivergenceLeader:
                return new("Macd", new[] { "Macd", "I1", "I2" }, bars =>
                {
                    // Linearity: A(x) + A(x-A(x)) = 2 A(x) - A(A(x)).
                    double[] Lead(int period)
                    {
                        var first = Average(Closes(bars), period, kind);
                        return first.Zip(Average(first, period, kind), (a, b) => 2 * a - b).ToArray();
                    }
                    var fast = Lead(Integer(options, "FastLength", 12));
                    var slow = Lead(Integer(options, "SlowLength", 26));
                    return Outputs(("Macd", fast.Zip(slow, (f, s) => f - s).ToArray()), ("I1", fast), ("I2", slow));
                });
            case IndicatorName.MirroredMovingAverageConvergenceDivergence:
                return new("Macd", new[] { "Macd", "Signal", "Histogram", "MirrorMacd", "MirrorSignal", "MirrorHistogram" }, bars =>
                {
                    // A(close)-A(open) = A(close-open); the mirror is its additive inverse.
                    var line = Average(bars.Select(b => b.Close - b.Open).ToArray(), Integer(options, "Length", 20), kind);
                    var signal = Average(line, Integer(options, "SignalLength", 9), kind);
                    var histogram = line.Zip(signal, (v, s) => v - s).ToArray();
                    return Outputs(("Macd", line), ("Signal", signal), ("Histogram", histogram),
                        ("MirrorMacd", line.Select(v => -v).ToArray()), ("MirrorSignal", signal.Select(v => -v).ToArray()),
                        ("MirrorHistogram", histogram.Select(v => -v).ToArray()));
                });
            case IndicatorName.ImpulseMovingAverageConvergenceDivergence:
                return new("Macd", new[] { "Macd", "Signal", "Histogram" }, bars =>
                {
                    var period = Integer(options, "Length", 34);
                    var middle = Average(bars.Select(b => (b.High + b.Low + b.Close) / 3).ToArray(), period, 4);
                    var high = Average(bars.Select(b => b.High).ToArray(), period, kind);
                    var low = Average(bars.Select(b => b.Low).ToArray(), period, kind);
                    var line = middle.Select((v, i) => v > high[i] ? v - high[i] : v < low[i] ? v - low[i] : 0).ToArray();
                    var signal = line.Select((_, i) => Window(line, i, Integer(options, "SignalLength", 9)).Average()).ToArray();
                    return Outputs(("Macd", line), ("Signal", signal), ("Histogram", line.Zip(signal, (v, s) => v - s).ToArray()));
                });
            case IndicatorName.LindaRaschke3_10Oscillator:
                return new("LindaMacd", new[] { "LindaMacd", "LindaMacdSignal", "LindaMacdHistogram", "LindaPpo", "LindaPpoSignal", "LindaPpoHistogram" }, bars =>
                {
                    var prices = Closes(bars);
                    var fast = Average(prices, Integer(options, "FastLength", 3), kind);
                    var slow = Average(prices, Integer(options, "SlowLength", 10), kind);
                    var difference = fast.Zip(slow, (f, s) => f - s).ToArray();
                    var percent = fast.Zip(slow, (f, s) => s == 0 ? 0 : 100 * (f / s - 1)).ToArray();
                    var period = Integer(options, "SmoothLength", 16);
                    var signal = Average(difference, period, kind);
                    var percentSignal = Average(percent, period, kind);
                    return Outputs(("LindaMacd", difference), ("LindaMacdSignal", signal),
                        ("LindaMacdHistogram", difference.Zip(signal, (v, s) => v - s).ToArray()),
                        ("LindaPpo", percent), ("LindaPpoSignal", percentSignal),
                        ("LindaPpoHistogram", percent.Zip(percentSignal, (v, s) => v - s).ToArray()));
                });
            case IndicatorName.MacZIndicator:
            case IndicatorName.MacZVwapIndicator:
                var volumeWeighted = indicator.BatchName == IndicatorName.MacZVwapIndicator;
                return new("Macz", new[] { "Macz", "Signal", "Histogram" }, bars =>
                {
                    var prices = Closes(bars);
                    var fast = Average(prices, Integer(options, "FastLength", 12), kind);
                    var slow = Average(prices, Integer(options, "SlowLength", 25), kind);
                    var period = Integer(options, volumeWeighted ? "Length2" : "Length", 25);
                    var variance = PopulationVariance(prices, period);
                    double[] line;
                    if (!volumeWeighted)
                    {
                        var baseline = Average(prices, period, 6);
                        var scale = Number(options, 1, "Mult");
                        line = prices.Select((value, i) => variance[i] == 0 ? 0
                            : scale * (value - baseline[i] + fast[i] - slow[i]) / Math.Sqrt(variance[i])).ToArray();
                    }
                    else
                    {
                        var volumePeriod = Integer(options, "Length1", 20);
                        var residual = prices.Select((value, i) =>
                        {
                            var window = Window(bars, i, volumePeriod).ToArray();
                            var volume = window.Sum(b => b.Volume);
                            // Centered weighted mean is exact on a constant-price window.
                            var mean = volume == 0 ? 0 : value + window.Sum(b => b.Volume * (b.Close - value)) / volume;
                            return value - mean;
                        }).ToArray();
                        var energy = Average(residual.Select(v => v * v).ToArray(), volumePeriod, 1);
                        var raw = prices.Select((_, i) => (energy[i] == 0 ? 0 : residual[i] / Math.Sqrt(energy[i]))
                            + (variance[i] == 0 ? 0 : (fast[i] - slow[i]) / Math.Sqrt(variance[i]))).ToArray();
                        line = LaguerreObservationWeights(raw, Number(options, .02, "Gamma"));
                    }
                    var signal = Average(line, Integer(options, "SignalLength", 9), kind);
                    return Outputs(("Macz", line), ("Signal", signal), ("Histogram", line.Zip(signal, (v, s) => v - s).ToArray()));
                });
            case IndicatorName.ImpulsePercentagePriceOscillator:
                return new("Ppo", new[] { "Ppo", "Signal", "Histogram" }, bars =>
                {
                    var period = Integer(options, "Length", 34);
                    var first = Average(bars.Select(b => (b.High + b.Low + b.Close) / 3).ToArray(), period, 3);
                    var second = Average(first, period, 3);
                    var high = Average(bars.Select(b => b.High).ToArray(), period, 6);
                    var low = Average(bars.Select(b => b.Low).ToArray(), period, 6);
                    var line = first.Select((value, i) =>
                    {
                        var middle = 2 * value - second[i];
                        var boundary = middle > high[i] ? high[i] : middle < low[i] ? low[i] : middle;
                        return boundary == 0 ? 0 : 100 * (middle / boundary - 1);
                    }).ToArray();
                    var signal = line.Select((_, i) => Window(line, i, 9).Average()).ToArray();
                    return Outputs(("Ppo", line), ("Signal", signal), ("Histogram", line.Zip(signal, (v, s) => v - s).ToArray()));
                });
            default: return null;
        }
    }

    private static double[] LaguerreObservationWeights(double[] values, double gamma)
    {
        // The four-stage Laguerre filter has a symmetric cubic numerator and denominator (1-gamma*z)^4.
        // Expand the denominator with the negative-binomial series and convolve the observation weights.
        var scale = (1 - gamma) * (1 - gamma) / 6;
        var a = scale * (1 - gamma + gamma * gamma);
        var b = scale * (2 - 5 * gamma + 2 * gamma * gamma);
        var numerator = new[] { a, b, b, a };
        var impulse = Enumerable.Range(0, values.Length).Select(i => Enumerable.Range(0, Math.Min(4, i + 1)).Sum(j =>
        {
            var lag = i - j;
            return numerator[j] * ((lag + 1d) * (lag + 2d) * (lag + 3d) / 6) * Math.Pow(gamma, lag);
        })).ToArray();
        return values.Select((_, i) => values[0] + Enumerable.Range(0, i + 1)
            .Sum(j => impulse[i - j] * (values[j] - values[0]))).ToArray();
    }
}
