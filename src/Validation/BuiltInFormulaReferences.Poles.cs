using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? PoleFilters(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName is IndicatorName.EhlersHighPassFilterV1 or IndicatorName.EhlersSimpleDecycler
            or IndicatorName.EhlersDecyclerOscillatorV1 or IndicatorName.EhlersDecycler)
        {
            var period = Integer(indicator.CreateOptions(), "Length", 125);
            var cutoffKey = indicator.BatchName switch
            {
                IndicatorName.EhlersHighPassFilterV1 => "Hp",
                IndicatorName.EhlersSimpleDecycler => "MiddleBand",
                IndicatorName.EhlersDecycler => "Ed",
                _ => "FastEdo"
            };
            var keys = cutoffKey == "MiddleBand" ? new[] { "MiddleBand", "UpperBand", "LowerBand" }
                : cutoffKey == "FastEdo" ? new[] { "FastEdo", "SlowEdo" } : new[] { cutoffKey };
            return new(cutoffKey, keys, bars =>
            {
                var prices = Closes(bars);
                if (cutoffKey == "Ed")
                {
                    var angle = 2 * Math.PI / Math.Max(2, period);
                    var pole = period <= 2 ? -1 : Math.Cos(angle) / (1 + Math.Sin(angle));
                    // Impulse response of (1-p)/2 * (1+z^-1)/(1-p*z^-1).
                    var result = prices.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j =>
                        (1 - pole) / 2 * Math.Pow(pole, i - j) * (prices[j] + (j == 0 ? 0 : prices[j - 1])))).ToArray();
                    return Outputs((cutoffKey, result));
                }
                var highPass = HighPassV1Trajectory(prices, period, 1);
                if (cutoffKey == "Hp") return Outputs((cutoffKey, highPass));
                var middle = prices.Zip(highPass, (p, h) => p - h).ToArray();
                if (cutoffKey == "MiddleBand") return Outputs((cutoffKey, middle),
                    ("UpperBand", middle.Select(v => v * 1.005).ToArray()), ("LowerBand", middle.Select(v => v * .995).ToArray()));
                var filtered = HighPassV1Trajectory(middle, period, .5);
                var slowHighPass = HighPassV1Trajectory(prices, period * 2, 1);
                var slowMiddle = prices.Zip(slowHighPass, (p, h) => p - h).ToArray();
                var slowFiltered = HighPassV1Trajectory(slowMiddle, period * 2, .5);
                return Outputs((cutoffKey, prices.Select((p, i) => p == 0 ? 0 : 120 * filtered[i] / p).ToArray()),
                    ("SlowEdo", prices.Select((p, i) => p == 0 ? 0 : 100 * slowFiltered[i] / p).ToArray()));
            });
        }
        if (indicator.BatchName == IndicatorName.EhlersChebyshevLowPassFilter)
        {
            var keys = Enumerable.Range(-2, 9).Select(wave => "Eclpf" + wave).ToArray();
            return new("Eclpf-2", keys, bars => ChebyshevTrajectories(Closes(bars)));
        }
        if (indicator.BatchName is IndicatorName.EhlersHighPassFilterV2 or IndicatorName.EhlersDecyclerOscillatorV2)
        {
            var options = indicator.CreateOptions();
            var kind = AverageKind(options, 2);
            if (kind == 0) return null;
            var oscillator = indicator.BatchName == IndicatorName.EhlersDecyclerOscillatorV2;
            var highPassKey = oscillator ? "Edo" : "Ehpf";
            return new(highPassKey, new[] { highPassKey }, bars =>
            {
                var prices = Closes(bars);
                var slow = HighPassV2Trajectory(prices, Integer(options, oscillator ? "SlowLength" : "Length", 20), kind);
                var result = oscillator ? slow.Zip(HighPassV2Trajectory(prices, Integer(options, "FastLength", 10), kind),
                    (left, right) => left - right).ToArray() : slow;
                return Outputs((highPassKey, result));
            });
        }
        var length = Math.Max(2, Integer(indicator.CreateOptions(), "Length", 14));
        var specification = indicator.BatchName switch
        {
            IndicatorName.Ehlers2PoleButterworthFilterV1 => ("E2bf", 2, 1.25, 0, 0),
            IndicatorName.Ehlers2PoleButterworthFilterV2 => ("E2bf", 2, 1d, 2, 3),
            IndicatorName.Ehlers3PoleButterworthFilterV1 => ("E3bf", 3, 1d, 0, 0),
            IndicatorName.Ehlers3PoleButterworthFilterV2 => ("E3bf", 3, 1d, 3, 4),
            IndicatorName.Ehlers2PoleSuperSmootherFilterV1 => ("Essf", 2, 1d, 0, 3),
            IndicatorName.Ehlers2PoleSuperSmootherFilterV2 => ("E2ssf", 2, 1d, 1, 0),
            IndicatorName.Ehlers3PoleSuperSmootherFilter => ("E3ssf", 3, 1d, 0, 4),
            _ => default
        };
        var (key, order, angleScale, binomialOrder, startup) = specification;
        if (key is null) return null;
        return new(key, new[] { key }, bars => Outputs((key,
            PoleTrajectory(Closes(bars), length, order, angleScale, binomialOrder, startup))));
    }

    private static double[] HighPassV1Trajectory(double[] prices, int length, double multiplier)
    {
        var period = Math.Sqrt(2) * multiplier * Math.Max(1, length);
        if (period <= 2) return new double[prices.Length];
        var angle = 2 * Math.PI / period;
        var pole = Math.Cos(angle) / (1 + Math.Sin(angle));
        var gain = (1 + pole) / 2;
        // Two cascaded first-order high-pass sections, rather than a second-order recurrence.
        var result = new double[prices.Length];
        double previousInput = 0, previousFirst = 0, previousSecond = 0;
        for (var i = 0; i < prices.Length; i++)
        {
            var first = gain * (prices[i] - previousInput) + pole * previousFirst;
            var second = gain * (first - previousFirst) + pole * previousSecond;
            result[i] = second;
            previousInput = prices[i];
            previousFirst = first;
            previousSecond = second;
        }
        return result;
    }

    private static double[] HighPassV2Trajectory(double[] prices, int length, int kind)
    {
        length = Math.Max(1, length);
        var angle = Math.Sqrt(2) * Math.PI / length;
        var pole = Complex.FromPolarCoordinates(Math.Exp(-angle), angle);
        var gain = ((1 + pole) * (1 + Complex.Conjugate(pole))).Real / 4;
        // Factor the denominator into conjugate complex first-order sections. The numerator is
        // the second difference; the published startup suppresses its first four samples.
        Complex first = 0, second = 0;
        var result = new double[prices.Length];
        for (var i = 4; i < prices.Length; i++)
        {
            first = gain * (prices[i] - 2 * prices[i - 1] + prices[i - 2]) + pole * first;
            second = first + Complex.Conjugate(pole) * second;
            result[i] = second.Real;
        }
        return Average(Average(result, length, kind), length, kind);
    }

    private static IReadOnlyDictionary<string, double[]> ChebyshevTrajectories(double[] prices)
    {
        // Fixed numerator and denominator polynomials of the nine published Chebyshev variants.
        var sections = new[]
        {
            (1.907, .293, .063, .513, .451, .481),
            (1.777, .731, .166, .977, 1.008, .561),
            (1.572, 1.026, .282, .356, 1.329, .644),
            (1.192, 1.281, .426, -.384, 1.565, .729),
            (.681, 1.46, .543, -.966, 1.703, .793),
            (.012, 1.606, .65, -1.408, 1.801, .848),
            (-.669, 1.716, .74, -1.685, 1.866, .89),
            (-1.226, 1.8, .811, -1.842, 1.91, .922),
            (-1.659, 1.873, .878, -1.957, 1.946, .951)
        };
        var outputs = new Dictionary<string, double[]>();
        for (var wave = 0; wave < sections.Length; wave++)
        {
            var (firstZero, firstSum, firstProduct, secondZero, secondSum, secondProduct) = sections[wave];
            Complex[] Roots(double sum, double product)
            {
                var discriminant = Complex.Sqrt(new Complex(sum * sum - 4 * product, 0));
                return new[] { (sum + discriminant) / 2, (sum - discriminant) / 2 };
            }
            var poles = Roots(firstSum, firstProduct).Concat(Roots(secondSum, secondProduct)).ToArray();
            var numerator = new[] { 1d, firstZero + secondZero, 2 + firstZero * secondZero, firstZero + secondZero, 1d };
            var mass = (2 + firstZero) * (2 + secondZero);
            var states = new Complex[4];
            var result = new double[prices.Length];
            for (var i = 0; i < result.Length; i++)
            {
                // A finite numerator followed by four normalized complex one-pole sections is independent
                // of the production pair of real second-order recurrences and its gain helper.
                Complex value = Enumerable.Range(0, Math.Min(5, i + 1)).Sum(lag => numerator[lag] * prices[i - lag]) / mass;
                for (var stage = 0; stage < states.Length; stage++)
                {
                    states[stage] += (1 - poles[stage]) * (value - states[stage]);
                    value = states[stage];
                }
                result[i] = value.Real;
            }
            outputs.Add("Eclpf" + (wave - 2), result);
        }
        return outputs;
    }

    // Solve the transfer function from its poles, independently of the production real recurrence.
    // Variant-specific angle constants and startup are part of the library's filter definitions.
    internal static double[] PoleTrajectory(double[] prices, int length, int order, double angleScale,
        int binomialOrder, int startup)
    {
        length = Math.Max(2, length);
        if (order == 2) return TwoPoleTrajectory(prices, length, angleScale, binomialOrder, startup);
        var decay = (order == 2 ? Math.Sqrt(2) : 1) * Math.PI / length;
        var angle = order == 2 ? decay * angleScale : 1.738 * Math.PI / length;
        var radius = BinaryDecimal(Math.Exp(-decay));
        var denominator = new[] { 1m, -2 * radius * BinaryDecimal(Math.Cos(angle)), radius * radius };
        if (order == 3)
        {
            var realPole = radius * radius;
            var quadratic = denominator;
            denominator = Enumerable.Range(0, 4).Select(k => (k < 3 ? quadratic[k] : 0)
                - (k > 0 ? realPole * quadratic[k - 1] : 0)).ToArray();
        }
        var gain = denominator.Sum();
        var impulse = new decimal[prices.Length];
        // Expand the reciprocal transfer polynomial in decimal. Partial fractions
        // subtract nearly equal poles at long periods, losing startup accuracy.
        for (var lag = 0; lag < impulse.Length; lag++)
        {
            impulse[lag] = lag == 0 ? 1 : -Enumerable.Range(1, Math.Min(lag, order))
                .Sum(k => denominator[k] * impulse[lag - k]);
        }
        var inputWeights = binomialOrder switch
        {
            1 => new[] { .5, .5 },
            2 => new[] { .25, .5, .25 },
            3 => new[] { .125, .375, .375, .125 },
            _ => new[] { 1d }
        };
        var exactPrices = prices.Select(BinaryDecimal).ToArray();
        var forcing = prices.Select((_, i) => gain * inputWeights.Select((weight, lag) =>
            lag <= i ? (decimal)weight * exactPrices[i - lag] : 0).Sum()).ToArray();
        var result = prices.Select((_, i) => Enumerable.Range(0, i + 1)
            .Sum(j => impulse[i - j] * forcing[j])).ToArray();
        // A startup override is an additional impulse at that bar; propagate its entire response.
        for (var i = 0; i < Math.Min(startup, prices.Length); i++)
        {
            var correction = (exactPrices[i] - result[i]) / impulse[0];
            for (var j = i; j < result.Length; j++) result[j] += correction * impulse[j - i];
        }
        return result.Select(v => (double)v).ToArray();
    }

    private static double[] TwoPoleTrajectory(double[] prices, int length, double angleScale,
        int binomialOrder, int startup)
    {
        // Complex first-order sections avoid the production real second-order recurrence.
        // Keep double's exponent range: decimal truncates decaying impulses near 1e-28,
        // which changes normalized oscillators even when the absolute error is tiny.
        var decay = Math.Sqrt(2) * Math.PI / length;
        var pole = Complex.FromPolarCoordinates(Math.Exp(-decay), decay * angleScale);
        var conjugate = Complex.Conjugate(pole);
        var gain = ((1 - pole) * (1 - conjugate)).Real;
        var weights = binomialOrder switch
        {
            1 => new[] { .5, .5 },
            2 => new[] { .25, .5, .25 },
            _ => new[] { 1d }
        };
        Complex first = 0, second = 0;
        var result = new double[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            var drive = weights.Select((weight, lag) => lag <= i ? weight * prices[i - lag] : 0).Sum();
            first = gain * drive + pole * first;
            second = first + conjugate * second;
            if (i < startup)
            {
                first += prices[i] - second;
                second = prices[i];
            }
            result[i] = second.Real;
        }
        return result;
    }
}
