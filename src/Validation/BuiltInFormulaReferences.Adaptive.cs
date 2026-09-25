using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? AdaptiveAverages(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 14);
        switch (indicator.BatchName)
        {
            case IndicatorName.ParametricCorrectiveLinearMovingAverage:
                return new("Pclma", new[] { "Pclma" }, bars => Outputs(("Pclma", bars.Select((_, i) =>
                {
                    var indices = Enumerable.Range(Math.Max(0, i - length + 1), Math.Min(length, i + 1)).ToArray();
                    // The typed contract fixes alpha=1 and per=35: signed affine weights on a period-lagged series.
                    var mass = indices.Sum(j => j + 1 - .35 * length);
                    return mass == 0 ? 0 : indices.Where(j => j >= length)
                        .Sum(j => (j + 1 - .35 * length) * bars[j - length].Close) / mass;
                }).ToArray())));
            case IndicatorName.EhlersKaufmanAdaptiveMovingAverage:
                return new("Ekama", new[] { "Ekama" }, bars =>
                {
                    var changes = bars.Select((b, i) => Math.Abs(b.Close - (i == 0 ? 0 : bars[i - 1].Close))).ToArray();
                    var line = new double[bars.Count];
                    for (var i = 0; i < line.Length; i++)
                    {
                        var travel = Window(changes, i, length).Sum();
                        var displacement = Math.Abs(bars[i].Close - (i < length - 1 ? 0 : bars[i - length + 1].Close));
                        var efficiency = travel == 0 ? 0 : Math.Min(1, displacement / travel);
                        var gain = Math.Pow(.0645 + .6667 * efficiency, 2);
                        var prior = i == 0 ? 0 : line[i - 1];
                        line[i] = prior + gain * (bars[i].Close - prior);
                    }
                    return Outputs(("Ekama", line));
                });
            case IndicatorName.EfficientPrice:
                return new("Ep", new[] { "Ep" }, bars =>
                {
                    var efficiency = EfficiencyRatios(bars, length);
                    var changes = bars.Select((b, i) => i < length ? 0 : (b.Close - bars[i - length].Close) * efficiency[i]).ToArray();
                    return Outputs(("Ep", changes.Select((_, i) => changes.Take(i + 1).Sum()).ToArray()));
                });
            case IndicatorName.EfficientAutoLine:
                return new("Eal", new[] { "Eal" }, bars =>
                {
                    var efficiency = EfficiencyRatios(bars, length);
                    var fast = Number(options, .0001, "FastAlpha");
                    var slow = Number(options, .005, "SlowAlpha");
                    var line = new double[bars.Count];
                    for (var i = 0; i < line.Length; i++)
                    {
                        var threshold = slow + efficiency[i] * (fast - slow);
                        line[i] = i < 9 || Math.Abs(bars[i].Close - line[i - 1]) > threshold ? bars[i].Close : line[i - 1];
                    }
                    return Outputs(("Eal", line));
                });
            case IndicatorName.WellRoundedMovingAverage:
                return new("Wrma", new[] { "Wrma" }, bars =>
                {
                    // Eliminate both integrators and the two smoothing states to obtain a cubic transfer function.
                    var alpha = 2d / (length + 1);
                    const double beta = .99, q = 1 - beta;
                    var gamma = Clamp(alpha, .01, .99);
                    var r = 1 - gamma;
                    var denominator = new[] { 1d, -(1 + q + r) + alpha * beta * (1 + gamma),
                        q + r + q * r - alpha * beta * r, -q * r };
                    var line = new double[bars.Count];
                    for (var i = 1; i < line.Length; i++)
                    {
                        var forcing = 2 * alpha * beta * (bars[i - 1].Close - (i < 2 ? 0 : r * bars[i - 2].Close));
                        line[i] = forcing - Enumerable.Range(1, Math.Min(3, i)).Sum(lag => denominator[lag] * line[i - lag]);
                    }
                    return Outputs(("Wrma", line));
                });
            case IndicatorName.VolatilityMovingAverage:
                var volatilityKind = AverageKind(options, 1);
                if (volatilityKind == 0) return null;
                var lookback = Integer(options, "LbLength", 10);
                var smooth = Integer(options, "SmoothLength", 3);
                return new("Vma", new[] { "Vma" }, bars =>
                {
                    var prices = Closes(bars);
                    var mean = Average(prices, lookback, volatilityKind);
                    var variance = PopulationVariance(prices, lookback);
                    var scores = prices.Select((p, i) => variance[i] == 0 ? 0 : 100 * (p - mean[i]) / Math.Sqrt(variance[i])).ToArray();
                    var smoothed = Average(scores, smooth, volatilityKind);
                    var variable = prices.Select((_, i) =>
                    {
                        var level = Math.Round(Math.Min(100, Math.Abs(smoothed[i])) / lookback);
                        var period = (int)Math.Round(Math.Max(1, length * (1 - level / 10)));
                        return Enumerable.Range(0, Math.Min(period, i + 1)).Sum(lag => (period - lag) * prices[i - lag])
                            / (period * (period + 1d) / 2);
                    }).ToArray();
                    return Outputs(("Vma", Average(variable, smooth, volatilityKind)));
                });
            case IndicatorName.UltimateMovingAverageBands:
            case IndicatorName.UltimateMovingAverage:
                var ultimateKind = AverageKind(options, 1);
                if (ultimateKind == 0) return null;
                var ultimateBands = indicator.BatchName == IndicatorName.UltimateMovingAverageBands;
                return new(ultimateBands ? "MiddleBand" : "Uma", ultimateBands ? new[] { "UpperBand", "MiddleBand", "LowerBand" } : new[] { "Uma" }, bars =>
                {
                    var maximum = Integer(options, "MaxLength", 50);
                    var minimum = Integer(options, "MinLength", 5);
                    var prices = Closes(bars);
                    var mean = Average(prices, maximum, ultimateKind);
                    var variance = PopulationVariance(prices, maximum);
                    var typical = bars.Select(b => (b.High + b.Low + b.Close) / 3).ToArray();
                    var positive = bars.Select((b, i) => i > 0 && typical[i] > typical[i - 1] ? typical[i] * b.Volume : 0).ToArray();
                    var negative = bars.Select((b, i) => i > 0 && typical[i] < typical[i - 1] ? typical[i] * b.Volume : 0).ToArray();
                    var line = new double[bars.Count];
                    var period = maximum;
                    for (var i = 0; i < bars.Count; i++)
                    {
                        if (variance[i] > 0)
                        {
                            var score = Math.Abs(prices[i] - mean[i]) / Math.Sqrt(variance[i]);
                            period = Clamp(period + (score <= .25 ? 1 : score > 1.75 ? -1 : 0), minimum, maximum);
                        }
                        var up = Window(positive, i, period).Sum();
                        var down = Window(negative, i, period).Sum();
                        var balance = down == 0 ? 1 : up == 0 ? -1 : (up - down) / (up + down);
                        var power = 1 + 4 * Math.Abs(balance);
                        var weights = Enumerable.Range(1, period).Select(age => Math.Exp(power * Math.Log(age))).ToArray();
                        line[i] = weights.Select((w, j) => i - period + 1 + j < 0 ? 0 : w * prices[i - period + 1 + j]).Sum() / weights.Sum();
                    }
                    if (!ultimateBands) return Outputs(("Uma", line));
                    var widths = PopulationVariance(prices, minimum).Select(v => Number(options, 2, "StdDevMult") * Math.Sqrt(v)).ToArray();
                    return Outputs(("UpperBand", line.Select((v, i) => v + widths[i]).ToArray()), ("MiddleBand", line),
                        ("LowerBand", line.Select((v, i) => v - widths[i]).ToArray()));
                });
            case IndicatorName.InverseDistanceWeightedMovingAverage:
                return new("Idwma", new[] { "Idwma" }, bars => Outputs(("Idwma", RoundedDistanceMassMean(bars, length))));
            case IndicatorName.OptimalWeightedMovingAverage:
                return new("Owma", new[] { "Owma" }, bars =>
                {
                    var line = new double[bars.Count];
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var window = Enumerable.Range(Math.Max(0, i - length + 1), Math.Min(i + 1, length)).ToArray();
                        var xMean = window.Average(j => bars[j].Close);
                        var yMean = window.Average(j => j == 0 ? 0 : line[j - 1]);
                        var x = window.Select(j => bars[j].Close - xMean).ToArray();
                        var y = window.Select(j => (j == 0 ? 0 : line[j - 1]) - yMean).ToArray();
                        var norm = Math.Sqrt(x.Sum(v => v * v) * y.Sum(v => v * v));
                        var correlation = norm == 0 ? 0 : x.Zip(y, (a, b) => a * b).Sum() / norm;
                        var weights = Enumerable.Range(0, length).Select(lag => Math.Exp(correlation * Math.Log(length - lag))).ToArray();
                        line[i] = weights.Select((w, lag) => i < lag ? 0 : w * bars[i - lag].Close).Sum() / weights.Sum();
                    }
                    return Outputs(("Owma", line));
                });
            case IndicatorName.WindowedVolumeWeightedMovingAverage:
                return new("Wvwma", new[] { "Wvwma" }, bars => Outputs(("Wvwma", RoundedWindowedVolumeMean(bars, length))));
            case IndicatorName.DoubleExponentialSmoothing:
                return new("Des", new[] { "Des" }, bars =>
                {
                    // The library's damped second-order smoother has transfer function
                    // alpha / (1 - (1-alpha)*(1+d)*z^-1 + (1-alpha)*d*z^-2), d=gamma*(2-gamma).
                    // Evaluate its conjugate first-order factors, independently of the output recurrence.
                    const double alpha = .01, damping = .9 * 1.1;
                    var sum = (1 - alpha) * (1 + damping);
                    var product = (1 - alpha) * damping;
                    var root = System.Numerics.Complex.Sqrt(new System.Numerics.Complex(sum * sum - 4 * product, 0));
                    var pole = (sum + root) / 2;
                    var other = (sum - root) / 2;
                    System.Numerics.Complex first = 0, second = 0;
                    var line = bars.Select(bar =>
                    {
                        first = alpha * bar.Close + pole * first;
                        second = first + other * second;
                        return second.Real;
                    }).ToArray();
                    return Outputs(("Des", line));
                });
            case IndicatorName.DynamicallyAdjustableMovingAverage:
                return new("Dama", new[] { "Dama" }, bars =>
                {
                    var prices = Closes(bars);
                    var fast = Integer(options, "FastLength", 6);
                    var slow = Integer(options, "SlowLength", 200);
                    var fastVariance = PopulationVariance(prices, fast);
                    var slowVariance = PopulationVariance(prices, slow);
                    var line = prices.Select((_, i) =>
                    {
                        var ratio = fastVariance[i] == 0 ? 0 : Math.Sqrt(slowVariance[i] / fastVariance[i]);
                        // The final slow-period cap also governs accepted reversed periods.
                        // When slow < fast this is the slow-period mean, matching the public formula.
                        var period = (int)Math.Round(Math.Min(slow, Math.Max(fast, fast + ratio)));
                        // Direct window mean, with missing startup observations contributing zero.
                        return Window(prices, i, period).Sum() / period;
                    }).ToArray();
                    return Outputs(("Dama", line));
                });
            case IndicatorName.DynamicallyAdjustableFilter:
                return new("Daf", new[] { "Daf" }, bars =>
                {
                    var source = new double[bars.Count];
                    var errorEnergy = new double[bars.Count];
                    var line = new double[bars.Count];
                    double gain = 0;
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var previous = i == 0 ? bars[i].Close : line[i - 1];
                        source[i] = 2 * bars[i].Close - previous;
                        line[i] = (1 - 2 * gain) * previous + 2 * gain * bars[i].Close;
                        var mean = Window(source, i, length).Average();
                        errorEnergy[i] = Math.Pow(source[i] - mean, 2);
                        var noise = length * Math.Sqrt(Window(errorEnergy, i, length).Average());
                        var discrepancy = Math.Abs(source[i] - line[i]);
                        gain = discrepancy == 0 ? 0 : 1 / (1 + noise / discrepancy);
                    }
                    return Outputs(("Daf", line));
                });
            case IndicatorName.VariableIndexDynamicAverage:
                return new("Vidya", new[] { "Vidya" }, bars => Outputs(("Vidya", RoundedVidya(bars, length))));
            case IndicatorName.VerticalHorizontalMovingAverage:
                return new("Vhma", new[] { "Vhma" }, bars =>
                {
                    var prices = Closes(bars);
                    // This variant uses length-bar displacement in its travel denominator.
                    var distance = prices.Select((value, i) => Math.Abs(value - (i < length ? 0 : prices[i - length]))).ToArray();
                    var gains = prices.Select((_, i) =>
                    {
                        var window = Window(prices, i, length).ToArray();
                        var travel = Window(distance, i, length).Sum();
                        return travel == 0 ? 0 : Math.Pow((window.Max() - window.Min()) / travel, 2);
                    }).ToArray();
                    return Outputs(("Vhma", ExpandedGainTrajectory(prices, gains)));
                });
            case IndicatorName._3HMA:
                var tripleKind = AverageKind(options, 2);
                if (tripleKind == 0) return null;
                var outerPeriod = Math.Max(1, (int)Math.Ceiling(length / 2d));
                var thirdPeriod = Math.Max(1, (int)Math.Ceiling(outerPeriod / 3d));
                var tripleHalfPeriod = Math.Max(1, (int)Math.Ceiling(outerPeriod / 2d));
                return new("3hma", new[] { "3hma" }, bars =>
                {
                    // Linearity permits filtering each leg twice before combining its gain.
                    var prices = Closes(bars);
                    var fast = Average(Average(prices, thirdPeriod, tripleKind), outerPeriod, tripleKind);
                    var middle = Average(Average(prices, tripleHalfPeriod, tripleKind), outerPeriod, tripleKind);
                    var slow = Average(Average(prices, outerPeriod, tripleKind), outerPeriod, tripleKind);
                    return Outputs(("3hma", fast.Select((value, i) => 3 * value - middle[i] - slow[i]).ToArray()));
                });
            case IndicatorName.VolatilityWaveMovingAverage:
                var waveKind = AverageKind(options, 2);
                if (waveKind == 0) return null;
                var waveSmooth = Math.Max(2, Math.Min(530, (int)Math.Ceiling(Math.Sqrt(length))));
                return new("Vwma", new[] { "Vwma" }, bars =>
                {
                    var prices = Closes(bars);
                    var variance = PopulationVariance(prices, length);
                    var weighted = prices.Select((price, i) =>
                    {
                        var relative = price == 0 ? 0 : 100 * Math.Sqrt(variance[i]) / price;
                        var exponent = relative < 0 ? 1 : Math.Max(1, Math.Min(4, 2.5 * Math.Sqrt(relative)));
                        var weights = Enumerable.Range(1, length).Select(j => Math.Pow(j / (double)length, exponent)).ToArray();
                        return Enumerable.Range(0, Math.Min(length, i + 1)).Sum(lag => prices[i - lag] * weights[length - lag - 1]) / weights.Sum();
                    }).ToArray();
                    var first = Average(weighted, waveSmooth, waveKind);
                    var second = Average(first, waveSmooth, waveKind);
                    return Outputs(("Vwma", first.Select((value, i) => 2 * value - second[i]).ToArray()));
                });
            case IndicatorName.VariableLengthMovingAverage:
                var variableKind = AverageKind(options, 1);
                if (variableKind == 0) return null;
                var variableMax = Integer(options, "MaxLength", 50);
                return new("Vlma", new[] { "Length", "Vlma" }, bars =>
                {
                    var prices = Closes(bars);
                    var mean = Average(prices, variableMax, variableKind);
                    var variance = PopulationVariance(prices, variableMax);
                    var periods = new double[bars.Count];
                    var result = new double[bars.Count];
                    double period = variableMax;
                    for (var i = 0; i < result.Length; i++)
                    {
                        if (variance[i] > 0)
                        {
                            var score = Math.Abs(prices[i] - mean[i]) / Math.Sqrt(variance[i]);
                            period += score <= .25 ? 1 : score > 1.75 ? -1 : 0;
                            period = Math.Max(length, Math.Min(variableMax, period));
                        }
                        periods[i] = period;
                        var previous = i == 0 ? prices[i] : result[i - 1];
                        result[i] = previous + 2 / (period + 1) * (prices[i] - previous);
                    }
                    return Outputs(("Length", periods), ("Vlma", result));
                });
            case IndicatorName.EquityMovingAverage:
                var equityKind = AverageKind(options, 1);
                if (equityKind == 0) return null;
                return new("Eqma", new[] { "Eqma" }, bars =>
                {
                    var mean = Average(Closes(bars), length, equityKind);
                    var positions = bars.Select((b, i) => Math.Sign(b.Close - mean[i])).ToArray();
                    var gains = bars.Select((b, i) => i == 0 ? 0 : (b.Close - bars[i - 1].Close) * positions[i - 1]).ToArray();
                    var hindsight = bars.Select((b, i) => i == 0 ? 0 : (b.Close - bars[i - 1].Close) * positions[i]).ToArray();
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var capital = hindsight.Take(i + 1).Sum();
                        var gain = capital == 0 ? .99 : Math.Max(.01, Math.Min(.99, Window(gains, i, length).Sum() / capital));
                        var previous = i == 0 ? bars[i].Close : result[i - 1];
                        result[i] = previous + gain * (bars[i].Close - previous);
                    }
                    return Outputs(("Eqma", result));
                });
            case IndicatorName.ElasticVolumeWeightedMovingAverageV1:
            case IndicatorName.ElasticVolumeWeightedMovingAverageV2:
                var elasticV1 = indicator.BatchName == IndicatorName.ElasticVolumeWeightedMovingAverageV1;
                var elasticKind = AverageKind(options, 1);
                if (elasticV1 && elasticKind == 0) return null;
                return new("Evwma", new[] { "Evwma" }, bars =>
                {
                    var volumes = bars.Select(b => b.Volume).ToArray();
                    var mass = elasticV1 ? Average(volumes, length, elasticKind).Select(v => 20 * v).ToArray()
                        : volumes.Select((_, i) => Window(volumes, i, length).Sum()).ToArray();
                    var gains = volumes.Select((v, i) => mass[i] <= 0 ? 0 : v / mass[i]).ToArray();
                    // Expand the variable-gain recurrence into explicit observation weights.
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        double retained = 1, displacement = 0;
                        for (var j = i; j > 0; j--)
                        {
                            displacement += retained * gains[j] * (bars[j].Close - bars[0].Close);
                            retained *= 1 - gains[j];
                        }
                        result[i] = bars[0].Close + displacement;
                    }
                    return Outputs(("Evwma", result));
                });
            case IndicatorName.EhlersZeroLagExponentialMovingAverage:
                var zeroLagKind = AverageKind(options, 3);
                if (zeroLagKind == 0) return null;
                var zeroLagDelay = (length - 1) / 2;
                return new("Ezlema", new[] { "Ezlema" }, bars => Outputs(("Ezlema", Average(bars.Select((b, i) =>
                    i < zeroLagDelay ? b.Close : 2 * b.Close - bars[i - zeroLagDelay].Close).ToArray(), length, zeroLagKind))));
            case IndicatorName.EhlersVariableIndexDynamicAverage:
                var vidyaKind = AverageKind(options, 2);
                if (vidyaKind == 0) return null;
                return new("Evidya", new[] { "Evidya" }, bars =>
                {
                    var prices = Closes(bars);
                    var shortMean = Average(prices, 9, vidyaKind);
                    var longMean = Average(prices, 30, vidyaKind);
                    var shortSquared = prices.Select((v, i) => Math.Pow(v - shortMean[i], 2)).ToArray();
                    var longSquared = prices.Select((v, i) => Math.Pow(v - longMean[i], 2)).ToArray();
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var shortEnergy = Window(shortSquared, i, 9).Average();
                        var longEnergy = Window(longSquared, i, 30).Average();
                        var gain = longEnergy == 0 ? 0 : Math.Max(.01, Math.Min(.99, .2 * Math.Sqrt(shortEnergy / longEnergy)));
                        var previous = i == 0 ? prices[i] : result[i - 1];
                        result[i] = (1 - gain) * previous + gain * prices[i];
                    }
                    return Outputs(("Evidya", result));
                });
            case IndicatorName.EhlersFractalAdaptiveMovingAverage:
                var framaPeriod = Math.Max(2, length);
                framaPeriod += framaPeriod % 2;
                return new("Fama", new[] { "Fama" }, bars =>
                {
                    // John Ehlers, FRAMA, Figure 1: https://www.mesasoftware.com/papers/FRAMA.pdf
                    // The library selects Close as its input; the paper's Price input is configurable.
                    var result = new double[bars.Count];
                    double dimension = 0;
                    for (var i = 0; i < result.Length; i++)
                    {
                        if (i + 1 >= framaPeriod)
                        {
                            var window = Window(bars, i, framaPeriod).ToArray();
                            var older = window.Take(framaPeriod / 2).ToArray();
                            var newer = window.Skip(framaPeriod / 2).ToArray();
                            var oldRange = older.Max(b => b.High) - older.Min(b => b.Low);
                            var newRange = newer.Max(b => b.High) - newer.Min(b => b.Low);
                            var fullRange = window.Max(b => b.High) - window.Min(b => b.Low);
                            if (oldRange > 0 && newRange > 0 && fullRange > 0)
                                dimension = Math.Log(2 * (oldRange + newRange) / fullRange, 2);
                        }
                        var gain = Math.Max(.01, Math.Min(1, Math.Exp(-4.6 * (dimension - 1))));
                        result[i] = i < framaPeriod ? bars[i].Close : result[i - 1] + gain * (bars[i].Close - result[i - 1]);
                    }
                    return Outputs(("Fama", result));
                });
            case IndicatorName.AdaptiveLeastSquares:
                return new("Als", new[] { "Als" }, bars =>
                {
                    var ranges = TrueRanges(bars);
                    var gains = ranges.Select((r, i) =>
                    {
                        var maximum = Window(ranges, i, length).Max();
                        return maximum == 0 ? .01 : Math.Max(.01, Math.Min(.99, Math.Pow(r / maximum, 1.5)));
                    }).ToArray();
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        // Explicit observation weights and normal equations, independent of the recursive moments.
                        var weights = new double[i + 1];
                        double retained = 1;
                        for (var j = i; j >= 0; j--)
                        {
                            weights[j] = retained * (j == 0 ? 1 : gains[j]);
                            retained *= 1 - gains[j];
                        }
                        var mass = weights.Sum();
                        var anchor = bars[i].Close;
                        var ageMean = Enumerable.Range(0, i + 1).Sum(j => weights[j] * (j - i)) / mass;
                        var priceMean = Enumerable.Range(0, i + 1).Sum(j => weights[j] * (bars[j].Close - anchor)) / mass;
                        var variance = Enumerable.Range(0, i + 1).Sum(j => weights[j] * Math.Pow(j - i - ageMean, 2));
                        var covariance = Enumerable.Range(0, i + 1).Sum(j => weights[j] * (j - i - ageMean) * (bars[j].Close - anchor - priceMean));
                        result[i] = anchor + priceMean - (variance == 0 ? 0 : ageMean * covariance / variance);
                    }
                    return Outputs(("Als", result));
                });
            case IndicatorName.AutonomousRecursiveMovingAverage:
                return new("Arma", new[] { "Arma" }, bars =>
                {
                    var deviations = new double[bars.Count];
                    var candidates = new double[bars.Count];
                    var firstMean = new double[bars.Count];
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var previous = i == 0 ? bars[i].Close : result[i - 1];
                        var lagged = i < 7 ? 0 : bars[i - 7].Close;
                        deviations[i] = Math.Abs(lagged - previous);
                        var radius = i == 0 ? 0 : 3 * deviations.Take(i + 1).Sum() / i;
                        var displacement = bars[i].Close - previous;
                        candidates[i] = Math.Abs(displacement) > radius ? bars[i].Close + Math.Sign(displacement) * radius : previous;
                        firstMean[i] = Window(candidates, i, length).Average();
                        result[i] = Window(firstMean, i, length).Average();
                    }
                    return Outputs(("Arma", result));
                });
            case IndicatorName.FareySequenceWeightedMovingAverage:
                return new("Fswma", new[] { "Fswma" }, bars => Outputs(("Fswma", RoundedFareyMean(bars, length))));
            case IndicatorName.FallingRisingFilter:
                return new("Frf", new[] { "Frf" }, bars =>
                {
                    var result = new double[bars.Count];
                    var alpha = 2d / (length + 1);
                    for (var i = 1; i < result.Length; i++)
                    {
                        var previousPrices = Enumerable.Range(Math.Max(0, i - length), Math.Min(i, length)).Select(j => bars[j].Close).ToList();
                        if (i < length) previousPrices.Add(0);
                        var escape = bars[i].Close > previousPrices.Max() || bars[i].Close < previousPrices.Min();
                        var gain = alpha + (escape ? 1 : alpha);
                        result[i] = (1 - gain) * result[i - 1] + gain * bars[i - 1].Close;
                    }
                    return Outputs(("Frf", result));
                });
            case IndicatorName.CoralTrendIndicator:
                var coralGain = 4d / (length + 3d);
                return new("Cti", new[] { "Cti" }, bars =>
                {
                    // Three generalized DEMA stages expand to Coral's six-pole polynomial.
                    // Each elementary smoother has zero initial state and fractional period.
                    double[] Smooth(double[] values) => values.Select((_, i) => Enumerable.Range(0, i + 1)
                        .Sum(j => coralGain * Math.Pow(1 - coralGain, i - j) * values[j])).ToArray();
                    var result = Closes(bars);
                    for (var stage = 0; stage < 3; stage++)
                    {
                        var first = Smooth(result);
                        var second = Smooth(first);
                        result = first.Select((v, i) => 1.4 * v - .4 * second[i]).ToArray();
                    }
                    return Outputs(("Cti", result));
                });
            case IndicatorName.CorrectedMovingAverage:
                var correctedKind = AverageKind(options, 1);
                if (correctedKind == 0) return null;
                return new("Cma", new[] { "Cma" }, bars =>
                {
                    var mean = Average(Closes(bars), length, correctedKind);
                    var variance = PopulationVariance(Closes(bars), length);
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        if (i < length) { result[i] = mean[i]; continue; }
                        var difference = mean[i] - result[i - 1];
                        // Nonnegative fixed point of k = v3*k*(2-k):
                        // k=max(0,1-variance/difference^2), then cancel one difference.
                        result[i] = variance[i] == 0 ? mean[i] : difference * difference <= variance[i]
                            ? result[i - 1] : mean[i] - variance[i] / difference;
                    }
                    return Outputs(("Cma", result));
                });
            case IndicatorName.BryantAdaptiveMovingAverage:
                var maximumLength = Integer(options, "MaxLength", 100);
                var trendParameter = Number(options, -1, "Trend");
                return new("Bama", new[] { "Bama" }, bars =>
                {
                    var efficiency = EfficiencyRatios(bars, length);
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var variableEfficiency = Math.Pow(1 + trendParameter * (efficiency[i] - .5), 2);
                        // Invert effective length + 1 directly, with the configured maximum length.
                        var gain = variableEfficiency == 0 ? 1 : Math.Min(1, Math.Max(2d / (maximumLength + 1), 2 * variableEfficiency / (length + 1)));
                        var previous = i == 0 ? 0 : result[i - 1];
                        result[i] = previous + gain * (bars[i].Close - previous);
                    }
                    return Outputs(("Bama", result));
                });
            case IndicatorName.AtrFilteredExponentialMovingAverage:
                var atrPeriod = Integer(options, "AtrLength", 20);
                var deviationPeriod = Integer(options, "StdDevLength", 10);
                var floorPeriod = Integer(options, "LbLength", 20);
                var gainCap = Number(options, 5, "Min");
                return new("Afp", new[] { "Afp" }, bars =>
                {
                    var ranges = TrueRanges(bars);
                    var normalized = ranges.Select((v, i) => bars[i].Close == 0 ? v : v / bars[i].Close).ToArray();
                    var atr = Average(normalized, atrPeriod, 1);
                    // Centered second moments avoid subtraction of nearly equal raw moments.
                    var deviation = PopulationVariance(atr, deviationPeriod).Select(Math.Sqrt).ToArray();
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var ratio = deviation[i] == 0 ? 1 : Window(deviation, i, floorPeriod).Min() / deviation[i];
                        var gain = 2 * Math.Min(ratio, gainCap) / (length + 1d);
                        var previous = i == 0 ? bars[i].Close : result[i - 1];
                        result[i] = previous + gain * (bars[i].Close - previous);
                    }
                    return Outputs(("Afp", result));
                });
            case IndicatorName.AdaptiveMovingAverage:
                var adaptiveFastGain = 2d / (Integer(options, "FastLength", 2) + 1d);
                var adaptiveSlowGain = 2d / (Integer(options, "SlowLength", 14) + 1d);
                return new("Ama", new[] { "Ama" }, bars =>
                {
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var window = Window(bars, i, length + 1).ToArray();
                        var high = window.Max(b => b.High);
                        var low = window.Min(b => b.Low);
                        var distance = high == low ? 0 : Math.Min(1, Math.Abs((bars[i].Close - low) / (high - low) - .5) * 2); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                        var gain = Math.Pow((1 - distance) * adaptiveSlowGain + distance * adaptiveFastGain, 2);
                        var previous = i == 0 ? 0 : result[i - 1];
                        result[i] = (1 - gain) * previous + gain * bars[i].Close;
                    }
                    return Outputs(("Ama", result));
                });
            case IndicatorName.PoweredKaufmanAdaptiveMovingAverage:
                var efficiencyPower = Number(options, 3, "Factor");
                return new("Pkama", new[] { "Pkama", "Per" }, bars =>
                {
                    var powered = EfficiencyRatios(bars, length).Select(v => Math.Pow(v, efficiencyPower)).ToArray();
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var previous = i == 0 ? bars[i].Close : result[i - 1];
                        result[i] = previous + powered[i] * (bars[i].Close - previous);
                    }
                    return Outputs(("Per", powered), ("Pkama", result));
                });
            case IndicatorName.AdaptiveExponentialMovingAverage:
                var adaptiveKind = AverageKind(options, 1);
                if (adaptiveKind == 0) return null;
                return new("Aema", new[] { "Aema" }, bars =>
                {
                    var initial = Average(Closes(bars), length, adaptiveKind);
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        if (i <= length) { result[i] = initial[i]; continue; }
                        var window = Window(bars, i, length).ToArray();
                        var high = window.Max(b => b.High);
                        var low = window.Min(b => b.Low);
                        var position = high == low ? 0 : Math.Min(1, 2 * Math.Abs(bars[i].Close - (high + low) / 2) / (high - low)); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                        var gain = 2d / (length + 1) * (1 + position);
                        result[i] = (1 - gain) * result[i - 1] + gain * bars[i].Close;
                    }
                    return Outputs(("Aema", result));
                });
            case IndicatorName.AdaptiveAutonomousRecursiveMovingAverage:
            case IndicatorName.AdaptiveAutonomousRecursiveTrailingStop:
                var bandScale = Number(options, 3, "Lambda", "Gamma");
                var trailing = indicator.BatchName == IndicatorName.AdaptiveAutonomousRecursiveTrailingStop;
                return new(trailing ? "Ts" : "Aarma", trailing ? new[] { "Ts" } : new[] { "D", "Aarma" }, bars =>
                {
                    var average = new double[bars.Count];
                    var width = new double[bars.Count];
                    var stop = new double[bars.Count];
                    var errors = new double[bars.Count];
                    var first = bars.Count == 0 ? 0 : bars[0].Close;
                    double upper = 0, lower = 0;
                    var rising = false;
                    for (var i = 0; i < average.Length; i++)
                    {
                        var price = bars[i].Close;
                        var previous = i == 0 ? price : average[i - 1];
                        errors[i] = Math.Abs(price - previous);
                        width[i] = i == 0 ? 0 : bandScale * errors.Skip(1).Take(i).Average();
                        var travel = i < length ? 0 : Enumerable.Range(i - length + 1, length)
                            .Sum(j => Math.Abs(bars[j].Close - bars[j - 1].Close));
                        var efficiency = travel == 0 ? 0 : Math.Abs(price - bars[i - length].Close) / travel;
                        var target = Math.Abs(price - previous) <= width[i] ? previous
                            : price + Math.Sign(price - previous) * width[i];
                        // Expand the two sequential smoothing stages to check their combined coefficients.
                        average[i] = (1 - efficiency) * previous + efficiency * (1 - efficiency) * first + efficiency * efficiency * target;
                        first += efficiency * (target - first);
                        if (price > upper) rising = true;
                        else if (price < lower) rising = false;
                        upper = average[i] + width[i];
                        lower = average[i] - width[i];
                        stop[i] = rising ? lower : upper;
                    }
                    return trailing ? Outputs(("Ts", stop)) : Outputs(("D", width), ("Aarma", average));
                });
            case IndicatorName.VolumeAdjustedMovingAverage:
                var volumeKind = AverageKind(options, 1);
                if (volumeKind == 0) return null;
                var volumeFactor = Number(options, .67, "Factor");
                return new("Vama", new[] { "Vama" }, bars =>
                {
                    // The common nonzero factor cancels from the normalized weighted mean.
                    var meanVolume = Average(bars.Select(b => (double)b.Volume).ToArray(), length, volumeKind);
                    var relativeVolume = bars.Select((b, i) => volumeFactor == 0 || meanVolume[i] == 0 ? 0 : (double)b.Volume / meanVolume[i]).ToArray();
                    return Outputs(("Vama", bars.Select((_, i) =>
                    {
                        var start = Math.Max(0, i - length + 1);
                        var indices = Enumerable.Range(start, i - start + 1).ToArray();
                        var denominator = indices.Sum(j => relativeVolume[j]);
                        return denominator == 0 ? 0 : indices.Sum(j => bars[j].Close * relativeVolume[j]) / denominator;
                    }).ToArray()));
                });
            case IndicatorName.VariableAdaptiveMovingAverage:
                var bodyKind = AverageKind(options, 1);
                if (bodyKind == 0) return null;
                return new("Vama", new[] { "Vama" }, bars =>
                {
                    // Linearity lets us smooth body and range directly instead of four OHLC streams.
                    var bodies = Average(bars.Select(b => b.Close - b.Open).ToArray(), length, bodyKind);
                    var ranges = Average(bars.Select(b => b.High - b.Low).ToArray(), length, bodyKind);
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var gain = ranges[i] == 0 ? 0 : Math.Max(.01, Math.Min(.99, Math.Abs(bodies[i]) / ranges[i]));
                        var previous = i == 0 ? bars[i].Close : result[i - 1];
                        result[i] = previous + gain * (bars[i].Close - previous);
                    }
                    return Outputs(("Vama", result));
                });
            case IndicatorName.CompoundRatioMovingAverage:
                var compoundKind = AverageKind(options, 2);
                if (compoundKind == 0) return null;
                var compoundBase = length == 1 ? 3 : 1 + 2 * Math.Exp((1d / (length - 1) - 1) * Math.Log(length));
                var compoundWeights = Enumerable.Range(0, length).Select(j => Math.Pow(compoundBase, -j)).ToArray();
                var compoundSum = compoundWeights.Sum();
                return new("Crma", new[] { "Crma" }, bars =>
                {
                    // Divide out the common base^length factor before evaluating the geometric kernel.
                    var raw = bars.Select((_, i) => Enumerable.Range(0, Math.Min(length, i + 1))
                        .Sum(j => bars[i - j].Close * compoundWeights[j]) / compoundSum).ToArray();
                    return Outputs(("Crma", Average(raw, Math.Max(1, (int)Math.Round(Math.Sqrt(length))), compoundKind)));
                });
            case IndicatorName.SequentiallyFilteredMovingAverage:
                var sequentialKind = AverageKind(options, 1);
                if (sequentialKind == 0) return null;
                return new("Sfma", new[] { "Sfma" }, bars =>
                {
                    var smooth = Average(Closes(bars), length, sequentialKind);
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var changes = Enumerable.Range(Math.Max(0, i - length + 1), Math.Min(length, i + 1))
                            .Select(j => smooth[j] - (j == 0 ? 0 : smooth[j - 1])).ToArray();
                        var monotone = changes.Length == length && (changes.All(v => v > 0) || changes.All(v => v < 0));
                        result[i] = monotone ? smooth[i] : i == 0 ? bars[i].Close : result[i - 1];
                    }
                    return Outputs(("Sfma", result));
                });
            case IndicatorName.TrueRangeAdjustedExponentialMovingAverage:
                var rangeMultiplier = Number(options, 1.5, "Mult");
                return new("Trema", new[] { "Trema" }, bars =>
                {
                    var range = TrueRanges(bars);
                    var smoothedRange = Average(range, length, 3);
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var ratio = smoothedRange[i] == 0 ? 1 : range[i] / smoothedRange[i];
                        var gain = 2d / (length + 1d) * Math.Min(2, rangeMultiplier * ratio);
                        var previous = i == 0 ? bars[i].Close : result[i - 1];
                        result[i] = (1 - gain) * previous + gain * bars[i].Close;
                    }
                    return Outputs(("Trema", result));
                });
            case IndicatorName.LightLeastSquaresMovingAverage:
                var lightKind = AverageKind(options, 1);
                if (lightKind == 0) return null;
                return new("Llsma", new[] { "Llsma" }, bars =>
                {
                    var full = Average(Closes(bars), length, lightKind);
                    var half = Average(Closes(bars), Math.Max(2, Math.Min(530, (length + 1) / 2)), lightKind);
                    var meanIndex = Average(Enumerable.Range(0, bars.Count).Select(i => (double)i).ToArray(), length, lightKind);
                    // Price deviation cancels from the two z-scores. The variance of consecutive
                    // integer indices is (n^2-1)/12, independent of the window's absolute index.
                    var indexDeviation = Math.Sqrt((length * (double)length - 1) / 12);
                    return Outputs(("Llsma", full.Select((v, i) => i + 1 < length || indexDeviation == 0 ? v
                        : v + (half[i] - v) * (i - meanIndex[i]) / indexDeviation).ToArray()));
                });
            case IndicatorName.DistanceWeightedMovingAverage:
                return new("Dwma", new[] { "Dwma" }, bars => Outputs(("Dwma", RoundedDistanceMassMean(bars, length, reciprocal: true))));
            case IndicatorName.MiddleHighLowMovingAverage:
                var middleKind = AverageKind(options, 3);
                if (middleKind == 0) return null;
                var midpointPeriod = Integer(options, "Length2", 10);
                var midpointSmoothing = Integer(options, "Length1", 14);
                return new("Mhlma", new[] { "Mhlma" }, bars => Outputs(("Mhlma", Average(bars.Select((_, i) =>
                {
                    var ordered = Window(bars, i, midpointPeriod).Select(b => b.Close).OrderBy(v => v).ToArray();
                    return (ordered[0] + ordered[ordered.Length - 1]) / 2;
                }).ToArray(), midpointSmoothing, middleKind))));
            case IndicatorName.RepulsionMovingAverage:
                var repulsionKind = AverageKind(options, 1);
                if (repulsionKind == 0) return null;
                return new("Rma", new[] { "Rma" }, bars =>
                {
                    var fast = Average(Closes(bars), length, repulsionKind);
                    var middle = Average(Closes(bars), 2 * length, repulsionKind);
                    var slow = Average(Closes(bars), 3 * length, repulsionKind);
                    return Outputs(("Rma", slow.Select((v, i) => v + middle[i] - fast[i]).ToArray()));
                });
            case IndicatorName.MovingAverageV3:
                var v3Kind = AverageKind(options, 3);
                if (v3Kind == 0) return null;
                var secondPeriod = Integer(options, "Length2", 3);
                var v3Gain = secondPeriod == 1 ? 0 : (length - 1d) / (secondPeriod - 1d);
                return new("Mav3", new[] { "Mav3" }, bars =>
                {
                    var first = Average(Closes(bars), length, v3Kind);
                    var second = Average(Closes(bars), secondPeriod, v3Kind);
                    return Outputs(("Mav3", first.Select((v, i) => v + v3Gain * (v - second[i])).ToArray()));
                });
            case IndicatorName.SelfWeightedMovingAverage:
                return new("Swma", new[] { "Swma" }, bars => Outputs(("Swma", bars.Select((_, i) =>
                {
                    // Pair the current window with the immediately preceding weight window.
                    if (i < length) return 0;
                    var first = Math.Max(0, i - 2 * length + 1);
                    var weights = Enumerable.Range(first, i - length - first + 1).ToArray();
                    var denominator = weights.Sum(j => bars[j].Close);
                    return denominator == 0 ? 0 : weights.Sum(j => bars[j].Close * bars[j + length].Close) / denominator;
                }).ToArray())));
            case IndicatorName.HoltExponentialMovingAverage:
                var levelGain = 2d / (Integer(options, "AlphaLength", length) + 1d);
                var trendGain = 2d / (Integer(options, "GammaLength", length) + 1d);
                return new("Hema", new[] { "Hema" }, bars =>
                {
                    var result = new double[bars.Count];
                    var level = 0d;
                    var trend = bars.Count == 0 ? 0 : bars[0].Close;
                    for (var i = 0; i < result.Length; i++)
                    {
                        // Holt's innovations form: correct the predicted level and trend
                        // with the same forecast error, instead of differencing level states.
                        var prediction = level + trend;
                        var error = bars[i].Close - prediction;
                        level = prediction + levelGain * error;
                        trend += levelGain * trendGain * error;
                        result[i] = level;
                    }
                    return Outputs(("Hema", result));
                });
            case IndicatorName.HullEstimate:
                var halfPeriod = Math.Max(2, Math.Min(530, (length + 1) / 2));
                return new("He", new[] { "He" }, bars =>
                {
                    var weighted = Average(Closes(bars), halfPeriod, 2);
                    var exponential = Average(Closes(bars), halfPeriod, 3);
                    return Outputs(("He", weighted.Select((v, i) => 3 * v - 2 * exponential[i]).ToArray()));
                });
            case IndicatorName.RecursiveMovingTrendAverage:
                var recursiveGain = 2d / (length + 1d);
                return new("Rmta", new[] { "Rmta" }, bars =>
                {
                    var result = new double[bars.Count];
                    var memory = bars.Count == 0 ? 0 : bars[0].Close;
                    var level = memory;
                    for (var i = 0; i < result.Length; i++)
                    {
                        var residual = bars[i].Close - recursiveGain * memory;
                        memory += residual;
                        level += recursiveGain * (bars[i].Close + residual - level);
                        result[i] = level;
                    }
                    return Outputs(("Rmta", result));
                });
            case IndicatorName.HampelFilter:
                var scalingFactor = Number(options, 3, "ScalingFactor");
                return new("Hf", new[] { "Hf" }, bars =>
                {
                    static double Median(IEnumerable<double> values)
                    {
                        var sorted = values.OrderBy(v => v).ToArray();
                        return (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
                    }
                    var result = new double[bars.Count];
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var window = Window(bars, i, length).Select(b => b.Close).ToArray();
                        var median = Median(window);
                        var mad = Median(window.Select(v => Math.Abs(v - median)));
                        var value = Math.Abs(bars[i].Close - median) > scalingFactor * mad ? median : bars[i].Close;
                        var previous = i == 0 ? 0 : result[i - 1];
                        result[i] = previous + 2d / (length + 1d) * (value - previous);
                    }
                    return Outputs(("Hf", result));
                });
            case IndicatorName.WellesWilderSummation:
                return new("Wws", new[] { "Wws" }, bars => Outputs(("Wws",
                    Average(Closes(bars), length, 6).Select(v => length * v).ToArray())));
            case IndicatorName.QuadraticMovingAverage:
                return new("Qma", new[] { "Qma" }, bars => Outputs(("Qma", RoundedRootMeanSquare(bars, length))));
            case IndicatorName.AlphaDecreasingExponentialMovingAverage:
                return new("Ema", new[] { "Ema" }, bars => Outputs(("Ema", bars.Select((b, i) =>
                    // alpha=2/(i+1): the first observation cancels at i=1; subsequent
                    // observations have weights proportional to their zero-based index.
                    i == 0 ? 2 * b.Close : Enumerable.Range(1, i).Sum(j => j * bars[j].Close)
                        / (i * (i + 1d) / 2)).ToArray())));
            case IndicatorName.AhrensMovingAverage:
                return new("Ahma", new[] { "Ahma" }, bars =>
                {
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var previous = i == 0 ? 0 : result[i - 1];
                        var delayed = i < length ? bars[i].Close : result[i - length];
                        result[i] = (1 - 1d / (2 * length)) * previous + (bars[i].Close - delayed / 2) / length;
                    }
                    return Outputs(("Ahma", result));
                });
            case IndicatorName.SharpModifiedMovingAverage:
                if (AverageKind(options, 1) == 0) return null;
                return new("Smma", new[] { "Smma" }, bars => AffineAverageOutputs(bars, indicator));
            case IndicatorName.SlowSmoothedMovingAverage:
                var slowKind = AverageKind(options, 2);
                if (slowKind == 0) return null;
                var middleWidth = (length + 2) / 3;
                var lastWidth = Math.Max(1, (length - middleWidth) / 2);
                var firstWidth = Math.Max(1, (length - middleWidth + 1) / 2);
                return new("Ssma", new[] { "Ssma" }, bars => Outputs(("Ssma",
                    Average(Average(Average(Closes(bars), firstWidth, slowKind), middleWidth, slowKind), lastWidth, slowKind))));
            case IndicatorName.RegularizedExponentialMovingAverage:
                var lambda = Number(options, .5, "Lambda");
                var smoothing = 2d / (length + 1d);
                return new("Rema", new[] { "Rema" }, bars =>
                {
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var previous = i == 0 ? 0 : result[i - 1];
                        var older = i < 2 ? 0 : result[i - 2];
                        // Minimizer of (y - EMA_step)^2 + lambda*(y - extrapolation)^2.
                        var emaStep = (1 - smoothing) * previous + smoothing * bars[i].Close;
                        var extrapolation = previous + (previous - older);
                        result[i] = (emaStep + lambda * extrapolation) / (1 + lambda);
                    }
                    return Outputs(("Rema", result));
                });
            case IndicatorName.ZeroLowLagMovingAverage:
                var lag = Number(options, 1.4, "Lag");
                return new("Zllma", new[] { "Zllma" }, bars =>
                {
                    var result = new double[bars.Count];
                    var corrected = new double[bars.Count];
                    var delay = (length + 1) / 2;
                    for (var i = 0; i < result.Length; i++)
                    {
                        var delayed = i < delay ? bars[i].Close : result[i - delay];
                        corrected[i] = bars[i].Close + (lag - 1) * (bars[i].Close - delayed);
                        result[i] = Window(corrected, i, length).Sum() / length;
                    }
                    return Outputs(("Zllma", result));
                });
            case IndicatorName.GeometricMeanMovingAverage:
                return new("Gmma", new[] { "Gmma" }, bars => Outputs(("Gmma", RoundedGeometricMean(bars, length, positiveOnly: true))));
            case IndicatorName.HarmonicMeanMovingAverage:
                return new("Hmma", new[] { "Hmma" }, bars => Outputs(("Hmma", HarmonicMeanReference(bars, length))));
            case IndicatorName.LeoMovingAverage:
                return new("Lma", new[] { "Lma" }, bars =>
                {
                    var prices = Closes(bars);
                    var simple = Average(prices, length, 1);
                    var weighted = Average(prices, length, 2);
                    return Outputs(("Lma", weighted.Select((v, i) => 2 * v - simple[i]).ToArray()));
                });
            case IndicatorName.GeneralizedDoubleExponentialMovingAverage:
                var generalizedKind = AverageKind(options, 3);
                if (generalizedKind == 0) return null;
                var generalizedFactor = Number(options, .7, "Factor", "VolumeFactor");
                return new("Gdema", new[] { "Gdema" }, bars =>
                {
                    var first = Average(Closes(bars), length, generalizedKind);
                    var second = Average(first, length, generalizedKind);
                    return Outputs(("Gdema", first.Select((v, i) => v + generalizedFactor * (v - second[i])).ToArray()));
                });
            case IndicatorName.EndPointMovingAverage:
                return new("Epma", new[] { "Epma" }, bars => AffineAverageOutputs(bars, indicator));
            case IndicatorName.JsaMovingAverage:
                return new("Jma", new[] { "Jma" }, bars => Outputs(("Jma", bars.Select((b, i) =>
                    (b.Close + (i >= length ? bars[i - length].Close : 0)) / 2).ToArray())));
            case IndicatorName.TillsonT3MovingAverage:
                var t3Kind = AverageKind(options, 3);
                if (t3Kind == 0) return null;
                var factor = Number(options, .7, "VFactor");
                return new("T3", new[] { "T3" }, bars =>
                {
                    // Three generalized DEMA stages, algebraically equivalent to the six-stage
                    // polynomial but derived without copying its expanded coefficients.
                    var result = Closes(bars);
                    for (var stage = 0; stage < 3; stage++)
                    {
                        var first = Average(result, length, t3Kind);
                        var second = Average(first, length, t3Kind);
                        result = first.Select((v, i) => v + factor * (v - second[i])).ToArray();
                    }
                    return Outputs(("T3", result));
                });
            case IndicatorName.QuadrupleExponentialMovingAverage:
            case IndicatorName.PentupleExponentialMovingAverage:
                var binomialKind = AverageKind(options, 3);
                if (binomialKind == 0) return null;
                // These historical names publish fifth- and eighth-order lag cancellation.
                var order = indicator.BatchName == IndicatorName.QuadrupleExponentialMovingAverage ? 5 : 8;
                var key = order == 5 ? "Qema" : "Pema";
                return new(key, new[] { key }, bars =>
                {
                    var residual = Closes(bars);
                    for (var stage = 0; stage < order; stage++)
                    {
                        var smooth = Average(residual, length, binomialKind);
                        residual = residual.Select((v, i) => v - smooth[i]).ToArray();
                    }
                    return Outputs((key, bars.Select((b, i) => b.Close - residual[i]).ToArray()));
                });
            case IndicatorName.ZeroLagTripleExponentialMovingAverage:
                var zeroKind = AverageKind(options, 5);
                if (zeroKind == 0) return null;
                return new("Ztema", new[] { "Ztema" }, bars =>
                {
                    var first = Average(Closes(bars), length, zeroKind);
                    var second = Average(first, length, zeroKind);
                    return Outputs(("Ztema", first.Select((v, i) => 2 * v - second[i]).ToArray()));
                });
            case IndicatorName.KaufmanAdaptiveMovingAverage:
                return new("Kama", new[] { "Kama", "Er" }, bars => RoundedKaufmanTrajectory(bars, length,
                    Integer(options, "FastLength", 2), Integer(options, "SlowLength", 30)));
            case IndicatorName.McGinleyDynamicIndicator:
                var k = Number(options, .6, "K");
                return new("Mdi", new[] { "Mdi" }, bars =>
                {
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var previous = i == 0 ? bars[i].Close : result[i - 1];
                        var denominator = previous == 0 ? 0 : k * length * Math.Pow(bars[i].Close / previous, 4);
                        var gain = 1 / Math.Max(1, denominator);
                        result[i] = denominator == 0 ? bars[i].Close : (1 - gain) * previous + gain * bars[i].Close;
                    }
                    return Outputs(("Mdi", result));
                });
            case IndicatorName.McNichollMovingAverage:
                var kind = AverageKind(options, 3);
                if (kind == 0 || length == 1) return null;
                return new("Mnma", new[] { "Mnma" }, bars =>
                {
                    var first = Average(Closes(bars), length, kind);
                    var second = Average(first, length, kind);
                    var alpha = 2d / (length + 1d);
                    return Outputs(("Mnma", first.Select((v, i) => v + (v - second[i]) / (1 - alpha)).ToArray()));
                });
            default: return null;
        }
    }
    private static double[] EfficiencyRatios(IReadOnlyList<Bar> bars, int period) => bars.Select((b, i) =>
    {
        if (i < period) return 0d;
        var travel = Enumerable.Range(i - period + 1, period).Sum(j => Math.Abs(bars[j].Close - bars[j - 1].Close));
        return travel == 0 ? 0 : Math.Abs(b.Close - bars[i - period].Close) / travel;
    }).ToArray();

    // Closed observation weights for a seeded time-varying first-order filter.
    private static double[] ExpandedGainTrajectory(double[] prices, double[] gains)
    {
        var result = new double[prices.Length];
        if (prices.Length == 0) return result;
        if (prices.All(value => value == prices[0])) return prices.ToArray(); // NOSONAR: S1244 - Only an exactly constant window takes the constant-series shortcut.
        for (var i = 0; i < prices.Length; i++)
        {
            double retained = 1, displacement = 0;
            for (var j = i; j > 0; j--)
            {
                displacement += retained * gains[j] * (prices[j] - prices[0]);
                retained *= 1 - gains[j];
            }
            result[i] = prices[0] + displacement;
        }
        return result;
    }

}
