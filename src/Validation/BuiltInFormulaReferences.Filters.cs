using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? Filters(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        switch (indicator.BatchName)
        {
            case IndicatorName.EhlersAdaptiveLaguerreFilter:
                return new("Ealf", new[] { "Ealf" }, bars =>
                {
                    var prices = Closes(bars);
                    var lookback = Integer(options, "Length", 14);
                    var differences = new double[prices.Length];
                    var ranks = new double[prices.Length];
                    var line = new double[prices.Length];
                    var stages = new double[4];
                    var gain = 2d / (lookback + 1);
                    for (var i = 0; i < prices.Length; i++)
                    {
                        var prior = i == 0 ? prices[i] : line[i - 1];
                        if (i == 0) for (var stage = 0; stage < stages.Length; stage++) stages[stage] = prices[i];
                        differences[i] = Math.Abs(prices[i] - prior);
                        var sample = Window(differences, i, lookback).ToArray();
                        var low = sample.Min(); var high = sample.Max();
                        // The public numerical contract treats deviations within 32 price-scale
                        // machine epsilons as ties, before normalization can amplify their noise.
                        var uncertainty = (32d / 4503599627370496d) * Math.Max(Math.Abs(prices[i]), Math.Abs(prior));
                        ranks[i] = high - low <= uncertainty || differences[i] - low <= uncertainty ? 0
                            : high - differences[i] <= uncertainty ? 1 : (differences[i] - low) / (high - low);
                        if (ranks[i] != 0)
                        {
                            var sorted = Window(ranks, i, 5).OrderBy(v => v).ToArray();
                            gain = (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
                        }
                        var next = new double[4];
                        next[0] = stages[0] + gain * (prices[i] - stages[0]);
                        for (var stage = 1; stage < 4; stage++)
                            next[stage] = stages[stage - 1] + (1 - gain) * (stages[stage] - next[stage - 1]);
                        line[i] = next.Select((v, j) => (j == 0 || j == 3 ? 1 : 2) * v).Sum() / 6;
                        stages = next;
                    }
                    return Outputs(("Ealf", line));
                });
            case IndicatorName.EdgePreservingFilter:
                var edgeLength = Integer(options, "Length", 200);
                var edgeKind = AverageKind(options, 1);
                if (edgeKind == 0) return null;
                return new("Epf", new[] { "Epf" }, bars =>
                {
                    var prices = Closes(bars);
                    var means = Average(prices, edgeLength, edgeKind);
                    var offsets = prices.Select((p, i) => p - means[i]).ToArray();
                    var fitted = RegressionEndpoints(offsets.Select(Math.Abs).ToArray(), 50);
                    var peaks = fitted.Select((v, i) =>
                    {
                        var peak = Window(fitted, i, edgeLength).Max();
                        return peak != 0 && Math.Abs(v - peak) <= 1e-12 * Math.Abs(peak);
                    }).ToArray();
                    var segmentStart = 0;
                    var seeded = true;
                    var line = new double[prices.Length];
                    for (var i = 0; i < line.Length; i++)
                    {
                        if (peaks[i] && (i == 0 || !peaks[i - 1]) && offsets[i] != 0)
                        {
                            segmentStart = i;
                            seeded = false;
                        }
                        // Before the first edge, the published filter includes a seed observation of price[0].
                        var observations = prices.Skip(segmentStart).Take(i - segmentStart + 1);
                        line[i] = seeded ? observations.Append(prices[0]).Average() : observations.Average();
                    }
                    return Outputs(("Epf", line));
                });
            case IndicatorName.EhlersAllPassPhaseShifter:
                var phaseLength = Integer(options, "Length", 20);
                return new("Eapps", new[] { "Eapps" }, bars =>
                {
                    const double radius = .5;
                    var cosine = Math.Cos(2 * Math.PI / phaseLength);
                    // Factor the transfer function into its driving polynomial and inverse denominator.
                    var impulse = new double[bars.Count];
                    for (var i = 0; i < impulse.Length; i++)
                        impulse[i] = (i == 0 ? 1 : 0) + (i > 0 ? 2 * radius * cosine * impulse[i - 1] : 0)
                            - (i > 1 ? radius * radius * impulse[i - 2] : 0);
                    var driven = bars.Select((b, i) => radius * radius * b.Close
                        - (i > 0 ? 2 * radius * cosine * bars[i - 1].Close : 0) + (i > 1 ? bars[i - 2].Close : 0)).ToArray();
                    return Outputs(("Eapps", bars.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j => impulse[j] * driven[i - j])).ToArray()));
                });
            case IndicatorName.ShapeshiftingMovingAverage:
            case IndicatorName.NarrowBandpassFilter:
                var finiteLength = Integer(options, "Length", 50);
                var bandpass = indicator.BatchName == IndicatorName.NarrowBandpassFilter;
                var finiteKey = bandpass ? "Nbpf" : "Sma";
                return new(finiteKey, new[] { finiteKey }, bars =>
                {
                    var weights = Enumerable.Range(0, finiteLength).Select(j =>
                    {
                        var x = (double)j / (finiteLength - 1);
                        // Blackman window factored in sin², independently of the cosine expansion.
                        var sineSquared = Math.Pow(Math.Sin(Math.PI * x), 2);
                        return bandpass ? Math.Sin(2 * Math.PI * j / finiteLength) * sineSquared * (.36 + .64 * sineSquared)
                            : (Math.Pow(x, 4) - 2 * x + 1) / (Math.Pow(x, 4) + 1);
                    }).ToArray();
                    var mass = bandpass ? 1 : weights.Sum();
                    return Outputs((finiteKey, bars.Select((_, i) => Enumerable.Range(0, Math.Min(i + 1, finiteLength))
                        .Sum(j => weights[j] * bars[i - j].Close) / mass).ToArray()));
                });
            case IndicatorName.ParametricKalmanFilter:
                var estimateLength = Integer(options, "Length", 50);
                return new("Pkf", new[] { "Pkf" }, bars =>
                {
                    var estimates = new double[bars.Count];
                    var uncertainty = 0d;
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var observation = bars[i].Close;
                        var prior = i == 0 ? observation : estimates[i - 1];
                        var baseline = i >= estimateLength ? estimates[i - estimateLength] : i == 0 ? observation : bars[i - 1].Close;
                        var residual = Math.Abs(observation - baseline);
                        var total = residual + uncertainty;
                        estimates[i] = total == 0 ? observation : (residual * prior + uncertainty * observation) / total;
                        uncertainty = total == 0 || i == 0 ? 0 : residual / total * Math.Abs(observation - bars[i - 1].Close);
                    }
                    return Outputs(("Pkf", estimates));
                });
            case IndicatorName.EhlersDistanceCoefficientFilter:
                var distanceLength = Integer(options, "Length", 14);
                return new("Edcf", new[] { "Edcf" }, bars =>
                {
                    var prices = Closes(bars);
                    var weights = new double[prices.Length];
                    for (var i = 0; i < prices.Length; i++)
                    {
                        if (distanceLength <= 1) continue;
                        var history = Enumerable.Range(1, distanceLength - 1).Select(lag => i >= lag ? prices[i - lag] : 0).ToArray();
                        var mean = history.Average();
                        // Sum squared distances = centered scatter + count * displacement squared.
                        weights[i] = history.Sum(value => Math.Pow(value - mean, 2))
                            + history.Length * Math.Pow(prices[i] - mean, 2);
                    }
                    var result = prices.Select((price, i) =>
                    {
                        var indices = Enumerable.Range(Math.Max(0, i - distanceLength + 1), Math.Min(i + 1, distanceLength));
                        var mass = indices.Sum(j => weights[j]);
                        return mass == 0 ? price : indices.Sum(j => prices[j] * weights[j]) / mass;
                    }).ToArray();
                    return Outputs(("Edcf", result));
                });
            case IndicatorName.EhlersHammingWindowIndicator:
            case IndicatorName.EhlersHannWindowIndicator:
            case IndicatorName.EhlersTriangleWindowIndicator:
            case IndicatorName.EhlersSimpleWindowIndicator:
                var windowLength = Integer(options, "Length", 20);
                var windowAverage = (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!;
                var windowKind = AverageKind(options, 1);
                if (windowKind == 0 && windowAverage != MovingAvgType.EhlersHammingMovingAverage
                    && windowAverage != MovingAvgType.EhlersHannMovingAverage && windowAverage != MovingAvgType.EhlersTriangleMovingAverage) return null;
                var simpleWindow = indicator.BatchName == IndicatorName.EhlersSimpleWindowIndicator;
                var windowKey = simpleWindow || indicator.BatchName == IndicatorName.EhlersTriangleWindowIndicator ? "Etwi" : "Ehwi";
                return new(windowKey, new[] { windowKey, "Roc" }, bars =>
                {
                    double[] Smooth(double[] values)
                    {
                        if (windowKind != 0) return Average(values, windowLength, windowKind);
                        var weights = Enumerable.Range(0, windowLength).Select(j => windowAverage switch
                        {
                            MovingAvgType.EhlersTriangleMovingAverage => (double)Math.Min(j + 1, windowLength - j),
                            MovingAvgType.EhlersHannMovingAverage => Math.Pow(Math.Sin(Math.PI * (j + 1) / (windowLength + 1)), 2),
                            _ => windowLength == 1 ? 1 : Math.Cos(3 - Math.PI / 2 + (Math.PI - 6) * j / (windowLength - 1))
                        }).ToArray();
                        var mass = weights.Sum();
                        return values.Select((_, i) => mass == 0 ? 0 : Enumerable.Range(0, Math.Min(windowLength, i + 1))
                            .Sum(j => weights[j] * values[i - j]) / mass).ToArray();
                    }
                    var filtered = Smooth(bars.Select(b => b.Close - b.Open).ToArray());
                    var differentiated = simpleWindow ? Smooth(Smooth(filtered)) : filtered;
                    var roc = differentiated.Select((v, i) => windowLength * Math.PI / 2 * (v - (i == 0 ? 0 : differentiated[i - 1]))).ToArray();
                    return Outputs((windowKey, filtered), ("Roc", roc));
                });
            case IndicatorName.EhlersSimpleDerivIndicator:
            case IndicatorName.EhlersSimpleClipIndicator:
                var clipped = indicator.BatchName == IndicatorName.EhlersSimpleClipIndicator;
                var derivativeLength = Integer(options, clipped ? "Length1" : "Length", 2);
                var rmsLength = Integer(options, "Length3", 50);
                var derivativeSignal = Integer(options, "SignalLength", clipped ? 22 : 8);
                var derivativeKind = AverageKind(options, 3);
                if (derivativeKind == 0) return null;
                var derivativeKey = clipped ? "Esci" : "Esdi";
                return new(derivativeKey, new[] { derivativeKey, "Signal" }, bars =>
                {
                    var differences = bars.Select((b, i) => i < derivativeLength ? 0 : b.Close - bars[i - derivativeLength].Close).ToArray();
                    var bounded = clipped ? differences.Select((v, i) =>
                    {
                        var energy = Window(differences, i, rmsLength).Sum(d => d * d) / rmsLength;
                        return energy == 0 ? 0 : Math.Max(-1, Math.Min(1, 2 * v / Math.Sqrt(energy)));
                    }).ToArray() : differences;
                    var line = bounded.Select((_, i) => Window(bounded, i, 4).Sum()).ToArray();
                    return Outputs((derivativeKey, line), ("Signal", Average(line, derivativeSignal, derivativeKind)));
                });
            case IndicatorName.EhlersNoiseEliminationTechnology:
                var netLength = Integer(options, "Length", 14);
                return new("Enet", new[] { "Enet" }, bars => Outputs(("Enet", bars.Select((_, i) =>
                {
                    if (netLength == 1) return 0d;
                    // Kendall's tau-a: rising minus falling pairs over all possible chronological pairs.
                    var observations = Enumerable.Range(i - netLength + 1, netLength).Select(j => j < 0 ? 0 : bars[j].Close).ToArray();
                    var rising = 0;
                    var falling = 0;
                    for (var end = 1; end < observations.Length; end++)
                    {
                        rising += observations.Take(end).Count(v => v < observations[end]);
                        falling += observations.Take(end).Count(v => v > observations[end]);
                    }
                    return 2d * (rising - falling) / (netLength * (netLength - 1d));
                }).ToArray())));
            case IndicatorName.EhlersSpearmanRankIndicator:
                var rankLength = Integer(options, "Length", 20);
                return new("Esri", new[] { "Esri" }, bars => Outputs(("Esri", bars.Select((_, i) =>
                {
                    var prices = Enumerable.Range(i - rankLength + 1, rankLength).Select(j => j < 0 ? 0 : bars[j].Close).ToArray();
                    // Obtain midranks by counting, independently of the production sort.
                    var ranks = prices.Select(v => prices.Count(p => p < v) + (prices.Count(p => p == v) - 1d) / 2).ToArray(); // NOSONAR: S1244 - Rank ties require equal observations.
                    var center = (rankLength - 1d) / 2;
                    var covariance = ranks.Select((r, j) => (r - center) * (j - center)).Sum();
                    var rankVariance = ranks.Sum(r => (r - center) * (r - center));
                    var timeVariance = Enumerable.Range(0, rankLength).Sum(j => (j - center) * (j - center));
                    return rankVariance == 0 || timeVariance == 0 ? 0 : covariance / Math.Sqrt(rankVariance * timeVariance);
                }).ToArray())));
            case IndicatorName.EhlersInstantaneousTrendlineV2:
                var trendAlpha = Number(options, .07, "Alpha");
                return new("Eit", new[] { "Eit", "Signal" }, bars =>
                {
                    double Price(int i) => i < 0 ? 0 : bars[i].Close;
                    double Seed(int i) => (Price(i) + 2 * Price(i - 1) + Price(i - 2)) / 4;
                    var pole = 1 - trendAlpha;
                    var a2 = trendAlpha * trendAlpha;
                    var line = bars.Select((_, i) =>
                    {
                        if (i < 7) return Seed(i);
                        var age = i - 6;
                        // Solve the repeated-pole recurrence from its two warmup conditions.
                        var homogeneous = ((age + 1) * Seed(6) - age * pole * Seed(5)) * Math.Pow(pole, age);
                        var driven = Enumerable.Range(7, i - 6).Sum(j => (i - j + 1) * Math.Pow(pole, i - j) *
                            ((trendAlpha - a2 / 4) * Price(j) + a2 / 2 * Price(j - 1) - (trendAlpha - .75 * a2) * Price(j - 2)));
                        return homogeneous + driven;
                    }).ToArray();
                    return Outputs(("Eit", line), ("Signal", line.Select((v, i) => 2 * v - (i < 2 ? 0 : line[i - 2])).ToArray()));
                });
            case IndicatorName.EhlersReflexIndicator:
            case IndicatorName.EhlersTrendflexIndicator:
                var flexLength = Integer(options, "Length", 20);
                var reflex = indicator.BatchName == IndicatorName.EhlersReflexIndicator;
                var flexKey = reflex ? "Eri" : "Eti";
                return new(flexKey, new[] { flexKey }, bars =>
                {
                    var angle = 2 * Math.Sqrt(2) * Math.PI / flexLength;
                    var radius = Math.Exp(-angle);
                    var feedback = 2 * radius * Math.Cos(angle);
                    var squaredRadius = radius * radius;
                    var feedforward = (1 - feedback + squaredRadius) / 2;
                    var filtered = new double[bars.Count];
                    double state1 = 0, state2 = 0;
                    for (var i = 0; i < filtered.Length; i++)
                    {
                        // Transposed state-space realization of the two-pole Super Smoother.
                        var value = bars[i].Close;
                        var output = feedforward * value + state1;
                        state1 = feedforward * value + feedback * output + state2;
                        state2 = -squaredRadius * output;
                        filtered[i] = output;
                    }
                    var deviations = filtered.Select((v, i) =>
                    {
                        var history = Enumerable.Range(1, flexLength).Select(j => i < j ? 0 : filtered[i - j]).ToArray();
                        // Reflex measures departures from the endpoint chord; Trendflex from a level.
                        var baseline = reflex ? v + (history[flexLength - 1] - v) * (flexLength + 1d) / (2 * flexLength) : v;
                        return baseline - history.Average();
                    }).ToArray();
                    return Outputs((flexKey, deviations.Select((v, i) =>
                    {
                        var energy = Enumerable.Range(0, i + 1).Sum(j => .04 * Math.Pow(.96, i - j) * deviations[j] * deviations[j]);
                        return energy == 0 ? 0 : v / Math.Sqrt(energy);
                    }).ToArray()));
                });
            case IndicatorName.EhlersGaussianFilter:
                var gaussianLength = Math.Max(2, Integer(options, "Length", 14));
                var gaussianPoles = Math.Max(1, Math.Min(4, Integer(options, "Poles", 3)));
                return new("Egf" + gaussianPoles, new[] { "Egf1", "Egf2", "Egf3", "Egf4" }, bars =>
                {
                    var outputs = new Dictionary<string, double[]>();
                    for (var poles = 1; poles <= 4; poles++)
                    {
                        var beta = (1 - Math.Cos(2 * Math.PI / gaussianLength)) / (Math.Pow(2, 1d / poles) - 1);
                        var alpha = Math.Sqrt(beta * beta + 2 * beta) - beta;
                        var kernel = Enumerable.Range(0, bars.Count).Select(lag =>
                        {
                            var multiplicity = 1d;
                            for (var k = 1; k < poles; k++) multiplicity *= (lag + k) / (double)k;
                            return multiplicity * Math.Pow(alpha, poles) * Math.Pow(1 - alpha, lag);
                        }).ToArray();
                        outputs["Egf" + poles] = bars.Select((_, i) => Enumerable.Range(0, i + 1)
                            .Sum(j => kernel[i - j] * bars[j].Close)).ToArray();
                    }
                    return outputs;
                });
            case IndicatorName.EhlersSpectrumDerivedFilterBank:
            case IndicatorName.EhlersRestoringPullIndicator:
                var bankMinimum = Integer(options, "MinLength", 8);
                var bankMaximum = Integer(options, "MaxLength", 50);
                var bankCutoff = Integer(options, "Length1", 40);
                var bankMedian = Integer(options, "Length2", 10);
                var restoring = indicator.BatchName == IndicatorName.EhlersRestoringPullIndicator;
                var pullKind = AverageKind(options, 3);
                if (restoring && pullKind == 0) return null;
                return new(restoring ? "Rpi" : "Esdfb", restoring ? new[] { "Rpi", "Signal" } : new[] { "Esdfb" }, bars =>
                {
                    var cycles = SpectrumCycles(bars, bankMinimum, bankMaximum, bankCutoff, bankMedian);
                    if (!restoring) return Outputs(("Esdfb", cycles));
                    return RestoringPullOutputs(bars, options);
                });
            case IndicatorName.EhlersLeadingIndicator:
                return new("Eli", new[] { "Eli" }, bars => Outputs(("Eli", bars.Select((_, i) =>
                    Enumerable.Range(0, i + 1).Sum(j =>
                    {
                        var lag = i - j;
                        // Partial fractions of .33*(2-1.75*z)/((1-.75*z)*(1-.67*z)).
                        var current = (Math.Pow(.75, lag + 1) - Math.Pow(.67, lag + 1)) / (.75 - .67);
                        var prior = lag == 0 ? 0 : (Math.Pow(.75, lag) - Math.Pow(.67, lag)) / (.75 - .67);
                        return .33 * (2 * current - 1.75 * prior) * bars[j].Close;
                    })).ToArray())));
            case IndicatorName.EhlersLaguerreRelativeStrengthIndex:
                var laguerreGamma = 1 - 2d / (Integer(options, "Length", 14) + 1d);
                return new("Elrsi", new[] { "Elrsi" }, bars =>
                {
                    var baseline = bars.Count == 0 ? 0 : bars[0].Close;
                    var first = bars.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j =>
                        (1 - laguerreGamma) * Math.Pow(laguerreGamma, i - j) * (bars[j].Close - baseline))).ToArray();
                    // Each following stage has the all-pass impulse response (-g, 1-g², g*(1-g²), ...).
                    double[] AllPass(double[] values) => values.Select((v, i) => -laguerreGamma * v +
                        Enumerable.Range(0, i).Sum(j => (1 - laguerreGamma * laguerreGamma) * Math.Pow(laguerreGamma, i - j - 1) * values[j])).ToArray();
                    var second = AllPass(first);
                    var third = AllPass(second);
                    var fourth = AllPass(third);
                    var line = bars.Select((_, i) =>
                    {
                        var differences = new[] { first[i] - second[i], second[i] - third[i], third[i] - fourth[i] };
                        var variation = differences.Sum(Math.Abs);
                        return variation == 0 ? 0 : differences.Sum(v => Math.Max(v, 0)) / variation;
                    }).ToArray();
                    return Outputs(("Elrsi", line));
                });
            case IndicatorName.EhlersLaguerreFilter:
                var gain = 2d / (Integer(options, "Length", 9) + 1d);
                var gamma = 1 - gain;
                // Combine the Laguerre all-pass stages as a rational transfer function:
                // gain*(D^3+2ND^2+2N^2D+N^3)/(6D^4), D=1-gamma*z, N=z-gamma.
                double[] Multiply(double[] left, double[] right)
                {
                    var result = new double[left.Length + right.Length - 1];
                    for (var i = 0; i < left.Length; i++)
                        for (var j = 0; j < right.Length; j++) result[i + j] += left[i] * right[j];
                    return result;
                }
                var denominatorBase = new[] { 1d, -gamma };
                var numeratorBase = new[] { -gamma, 1d };
                var numerator = new double[4];
                for (var power = 0; power < 4; power++)
                {
                    var polynomial = new[] { 1d };
                    for (var j = 0; j < 3; j++) polynomial = Multiply(polynomial, j < power ? numeratorBase : denominatorBase);
                    for (var j = 0; j < 4; j++) numerator[j] += gain / 6 * (power == 0 || power == 3 ? 1 : 2) * polynomial[j];
                }
                var denominator = Multiply(Multiply(denominatorBase, denominatorBase), Multiply(denominatorBase, denominatorBase));
                return new("Elf", new[] { "Elf" }, bars =>
                {
                    var baseline = bars.Count == 0 ? 0 : bars[0].Close;
                    var result = new double[bars.Count];
                    var delays = new double[4];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var input = bars[i].Close - baseline;
                        var output = numerator[0] * input + delays[0];
                        for (var j = 0; j < 4; j++)
                            delays[j] = (j < 3 ? numerator[j + 1] * input + delays[j + 1] : 0) - denominator[j + 1] * output;
                        result[i] = baseline + output;
                    }
                    return Outputs(("Elf", result));
                });
            case IndicatorName.EhlersFiniteImpulseResponseFilter:
                // Seven published FIR taps, expressed as integer ratios.
                var taps = new[] { 2d, 7, 9, 6, 1, -1, -3 };
                return new("Efirf", new[] { "Efirf" }, bars => Outputs(("Efirf", bars.Select((_, i) =>
                    Enumerable.Range(0, Math.Min(taps.Length, i + 1)).Sum(j => taps[j] * bars[i - j].Close) / 21).ToArray())));
            case IndicatorName.EhlersInfiniteImpulseResponseFilter:
                var length = Integer(options, "Length", 14);
                var lag = Math.Max(2, Math.Min(530, length / 2));
                var alpha = 2d / (length + 1d);
                return new("Eiirf", new[] { "Eiirf" }, bars =>
                {
                    var result = new double[bars.Count];
                    double level = 0;
                    for (var i = 0; i < result.Length; i++)
                    {
                        var input = bars[i].Close + (i < lag ? 0 : bars[i].Close - bars[i - lag].Close);
                        level += alpha * (input - level);
                        result[i] = level;
                    }
                    return Outputs(("Eiirf", result));
                });
            case IndicatorName.EhlersOptimumEllipticFilter:
            case IndicatorName.EhlersModifiedOptimumEllipticFilter:
                var modified = indicator.BatchName == IndicatorName.EhlersModifiedOptimumEllipticFilter;
                return new("Emoef", new[] { "Emoef" }, bars => Outputs(("Emoef", EllipticTrajectory(Closes(bars), modified))));
            default: return null;
        }
    }
    private static double[] EllipticTrajectory(double[] values, bool modified)
    {
        // H(z)=(.13785+.0007z^-1+.13785z^-2)/(1-1.2103z^-1+.4867z^-2).
        // Transposed direct form II; the modified filter adds (2-z^-1) and a constant-price seed.
        var result = new double[values.Length];
        var baseline = modified && values.Length > 0 ? values[0] : 0;
        double state1 = 0, state2 = 0, previousInput = 0;
        for (var i = 0; i < result.Length; i++)
        {
            var centered = values[i] - baseline;
            var input = modified ? 2 * centered - previousInput : centered;
            previousInput = centered;
            var output = .13785 * input + state1;
            state1 = .0007 * input + 1.2103 * output + state2;
            state2 = .13785 * input - .4867 * output;
            result[i] = output + baseline;
        }
        return result;
    }

}
