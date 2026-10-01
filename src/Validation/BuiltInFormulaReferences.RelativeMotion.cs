using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static double[] PrimeOffsetsReference(double[] prices, int tolerance)
    {
        // Direct divisor definition, independent of the production six-step primality test.
        bool Prime(long n)
        {
            if (n < 2) return false;
            for (long divisor = 2; divisor <= Math.Floor(Math.Sqrt(n)); divisor++)
                if (n % divisor == 0) return false;
            return true;
        }
        double previousUpper = 0, previousLower = 0, previousOffset = 0;
        return prices.Select(value =>
        {
            var center = (long)Math.Round(value);
            var radius = value * tolerance / 100;
            var high = (long)Math.Round(value + radius);
            var low = (long)Math.Round(value - radius);
            for (var candidate = center; candidate <= high; candidate++)
            {
                if (Prime(candidate)) { previousUpper = candidate; break; }
                if (candidate == long.MaxValue) break;
            }
            for (var candidate = center; candidate >= Math.Max(2, low); candidate--)
                if (Prime(candidate)) { previousLower = candidate; break; }
            var offset = previousUpper - value < value - previousLower ? previousUpper - value : previousLower - value;
            if (offset != 0) previousOffset = offset;
            return previousOffset;
        }).ToArray();
    }

    private static double[] DepthCorrectionReference(double[] prices, double[] feedback)
    {
        var impulse = new double[prices.Length];
        var gain = 1 - feedback.Sum();
        for (var i = 0; i < impulse.Length; i++)
            impulse[i] = i == 0 ? 1 : Enumerable.Range(1, Math.Min(i, feedback.Length)).Sum(lag => feedback[lag - 1] * impulse[i - lag]);
        // Missing alpha history is seeded with that bar's price; beta history is zero.
        var forcing = prices.Select((price, i) => price * (gain + feedback.Skip(i).Sum())).ToArray();
        var alpha = prices.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j => forcing[j] * impulse[i - j])).ToArray();
        return prices.Select((_, i) => alpha[i] + gain / feedback.Length * Enumerable.Range(0, i + 1)
            .Sum(j => (prices[j] - alpha[j]) * impulse[i - j])).ToArray();
    }

    private static double[] SecondOrderImpulse(int count, double first, double second)
    {
        var impulse = new double[count];
        for (var i = 0; i < count; i++)
            impulse[i] = i == 0 ? 1 : first * impulse[i - 1] + (i > 1 ? second * impulse[i - 2] : 0);
        return impulse;
    }

    private static double[] QuadraticProjectionReference(double[] prices, int length, int kind)
    {
        var indices = Enumerable.Range(0, prices.Length).Select(i => (double)i).ToArray();
        var xMean = Average(indices, length, kind); var qMean = Average(indices.Select(x => x * x).ToArray(), length, kind);
        var yMean = Average(prices, length, kind);
        return prices.Select((_, i) =>
        {
            if (length < 3 || i < 2) return yMean[i];
            // Orthogonalize 1, x, x^2 directly, using decimal arithmetic so the oracle
            // does not share the production raw-moment determinant's cancellation.
            var x = Enumerable.Range(i - length + 1, length).Select(j => (decimal)Math.Max(0, j)).ToArray();
            var y = Enumerable.Range(i - length + 1, length).Select(j => j < 0 ? 0m : (decimal)prices[j]).ToArray();
            var xm = x.Average(); var qm = x.Average(v => v * v);
            var u = x.Select(v => v - xm).ToArray(); var u2 = u.Sum(v => v * v);
            var projection = x.Select((v, j) => u[j] * (v * v - qm)).Sum() / u2;
            var v = x.Select((value, j) => value * value - qm - projection * u[j]).ToArray();
            var curvature = v.Select((value, j) => value * y[j]).Sum() / v.Sum(value => value * value);
            var slope = u.Select((value, j) => value * y[j]).Sum() / u2 - projection * curvature;
            return yMean[i] + (double)slope * (i - xMean[i]) + (double)curvature * (i * (double)i - qMean[i]);
        }).ToArray();
    }

    private static FormulaDefinition? RelativeMotion(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 14);
        var kind = AverageKind(options, 3);
        if (kind == 0) return null;
        switch (name)
        {
            case IndicatorName.HalfTrend:
                return new("Ht", new[] { "Ht" }, bars => HalfTrendOutputs(bars, indicator));
            case IndicatorName.Trender:
                return new("Trender", new[] { "TrendUp", "TrendDn", "Trender" }, bars => TrenderOutputs(bars, indicator));
            case IndicatorName.SqueezeMomentumIndicator:
                return new("Smi", new[] { "Smi" }, bars =>
                {
                    var prices = Closes(bars);
                    var average = Average(prices, length, kind);
                    var residual = prices.Select((price, i) =>
                    {
                        var window = Window(bars, i, length).ToArray();
                        var rangeCenter = (window.Max(b => b.High) + window.Min(b => b.Low)) / 2;
                        return price - (rangeCenter + average[i]) / 2;
                    }).ToArray();
                    return Outputs(("Smi", RegressionEndpoints(residual, length)));
                });
            case IndicatorName.PeakValleyEstimation:
                return new("Sign1", new[] { "Sign1", "Sign2", "Sign3" }, bars =>
                {
                    var prices = Closes(bars); var average = Average(prices, length, kind);
                    var offset = prices.Select((v, i) => v - average[i]).ToArray();
                    var fit = RegressionEndpoints(offset.Select(Math.Abs).ToArray(), Integer(options, "SmoothLength", 100));
                    var ratios = fit.Select((v, i) =>
                    {
                        var high = Window(fit, i, Math.Max(2, length)).Max();
                        return high == 0 ? 0 : v / high;
                    }).ToArray();
                    double[] Signs(Func<int, bool> condition) => offset.Select((v, i) => condition(i) ? -(double)Math.Sign(v) : 0).ToArray();
                    return Outputs(("Sign1", Signs(i => ratios[i] == 1 && (i == 0 || ratios[i - 1] != 1))), // NOSONAR: S1244 - The signal contract detects exact visits to the normalized maximum.
                        ("Sign2", Signs(i => ratios[i] < .8)), ("Sign3", Signs(i => i > 0 && ratios[i - 1] == 1 && ratios[i] < 1))); // NOSONAR: S1244 - The signal contract detects departure from the normalized maximum.
                });
            case IndicatorName.StationaryExtrapolatedLevelsOscillator:
                return new("Selo", new[] { "Selo" }, bars =>
                {
                    var prices = Closes(bars); var average = Average(prices, length, kind);
                    var residual = prices.Select((v, i) => v - average[i]).ToArray();
                    var extrapolated = prices.Select((_, i) => (i < length ? 0 : residual[i - length])
                        - (i < 2 * length ? 0 : residual[i - 2 * length] / 2)).ToArray();
                    // A transformed input inside its candle retains that candle's range;
                    // otherwise its range spans its current and previous observations.
                    var bounds = extrapolated.Select((v, i) =>
                    {
                        var tolerance = 1e-12 * Math.Max(Math.Abs(bars[i].Low), Math.Abs(bars[i].High));
                        var inside = v >= bars[i].Low - tolerance && v <= bars[i].High + tolerance;
                        var previous = i == 0 ? v : extrapolated[i - 1];
                        return (Low: inside ? bars[i].Low : Math.Min(previous, v), High: inside ? bars[i].High : Math.Max(previous, v));
                    }).ToArray();
                    return Outputs(("Selo", extrapolated.Select((v, i) =>
                    {
                        var sample = Window(bounds, i, 2 * length).ToArray(); var low = sample.Min(b => b.Low); var high = sample.Max(b => b.High);
                        return high == low ? 0 : Clamp(100 * (v - low) / (high - low), 0, 100); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }).ToArray()));
                });
            case IndicatorName.FreedomOfMovement:
                return new("Fom", new[] { "Fom", "Dpl" }, bars =>
                {
                    var volume = bars.Select(b => b.Volume).ToArray(); var average = Average(volume, length, kind);
                    double Deviation(double[] values, int i)
                    {
                        if (i + 1 < length) return 0;
                        var sample = Window(values, i, length).ToArray(); var mean = sample.Average();
                        return Math.Sqrt(sample.Average(v => (v - mean) * (v - mean)));
                    }
                    var relative = volume.Select((v, i) => Deviation(volume, i) == 0 ? 0 : (v - average[i]) / Deviation(volume, i)).ToArray();
                    var movement = bars.Select((b, i) => i == 0 || bars[i - 1].Close == 0 ? 0 : Math.Abs(b.Close / bars[i - 1].Close - 1)).ToArray();
                    double Rank(double[] values, int i)
                    {
                        var sample = Window(values, i, length).ToArray(); var low = sample.Min(); var high = sample.Max();
                        return high == low ? 0 : 1 + 9 * ((values[i] - low) / (high - low)); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }
                    var ratios = movement.Select((_, i) => Rank(movement, i) == 0 ? 0 : Rank(relative, i) / Rank(movement, i)).ToArray();
                    var scores = ratios.Select((v, i) => Deviation(ratios, i) == 0 ? 0 : (v - Window(ratios, i, length).Average()) / Deviation(ratios, i)).ToArray();
                    var triggers = Enumerable.Range(0, bars.Count).Where(i => scores[i] >= 2).ToArray();
                    var line = bars.Select((b, i) =>
                    {
                        var last = triggers.Where(j => j <= i).DefaultIfEmpty(-1).Max();
                        return last < 0 ? bars[0].Close : last == 0 ? 0 : bars[last - 1].Close;
                    }).ToArray();
                    return Outputs(("Fom", scores), ("Dpl", line));
                });
            case IndicatorName.RSINGIndicator:
                return new("Rsing", new[] { "Rsing", "Signal" }, bars =>
                {
                    var volume = Average(bars.Select(b => b.Volume).ToArray(), length, kind);
                    var ranges = bars.Select(b => b.High - b.Low).ToArray();
                    var momentum = bars.Select((b, i) =>
                    {
                        if (i < length || volume[i] == 0) return 0d;
                        var sample = Window(ranges, i, length).ToArray();
                        // Independent pair-distance identity: variance = sum_{j<k}(xj-xk)^2/n^2.
                        // Rounding a mean near a constant range changes this small denominator materially.
                        double squaredDistances = 0;
                        for (var j = 0; j < sample.Length; j++)
                            for (var k = j + 1; k < sample.Length; k++)
                                squaredDistances += (sample[j] - sample[k]) * (sample[j] - sample[k]);
                        var sigma = Math.Sqrt(squaredDistances / ((double)sample.Length * sample.Length));
                        return sigma == 0 ? 0 : (b.Close - bars[i - length].Close) * b.Volume * ranges[i] / (volume[i] * sigma);
                    }).ToArray();
                    return Outputs(("Rsing", momentum), ("Signal", Average(momentum, length, kind)));
                });
            case IndicatorName.QuadraticRegression:
                return new("QuadReg", new[] { "QuadReg" }, bars => Outputs(("QuadReg", QuadraticProjectionReference(Closes(bars), length, kind))));
            case IndicatorName.LinearQuadraticConvergenceDivergenceOscillator:
                return new("Lqcdo", new[] { "Lqcdo" }, bars =>
                {
                    var prices = Closes(bars); var linear = RegressionEndpoints(prices, length);
                    var quadratic = QuadraticProjectionReference(prices, length, 1);
                    var difference = quadratic.Select((v, i) => v - linear[i]).ToArray();
                    var signal = Average(difference, 25, 1);
                    return Outputs(("Lqcdo", difference.Select((v, i) => v - 2 * signal[i]).ToArray()));
                });
            case IndicatorName.HerrickPayoffIndex:
                return new("Hpi", new[] { "Hpi" }, bars => Outputs(("Hpi", bars.Select((b, i) =>
                {
                    if (i == 0) return 0d;
                    var change = ((b.High + b.Low) - (bars[i - 1].High + bars[i - 1].Low)) / 2;
                    var opening = Math.Min(b.Open, bars[i - 1].Open);
                    var adjustment = opening == 0 ? 0 : Math.Abs(b.Close - bars[i - 1].Close) / (2 * opening);
                    return change * b.Volume * Number(options, 100, "PointValue") * (1 + (change < 0 ? -adjustment : adjustment));
                }).ToArray())));
            case IndicatorName.PivotDetectorOscillator:
                return new("Pdo", new[] { "Pdo" }, bars =>
                {
                    // The legacy Length option is inert; the published periods are 200 and 14.
                    var prices = Closes(bars); var level = Average(prices, 200, kind); var strength = MotionRsi(prices, 14, kind);
                    return Outputs(("Pdo", prices.Select((v, i) => 2 * strength[i] - (v > level[i] ? 70 : 40)).ToArray()));
                });
            case IndicatorName.TopsAndBottomsFinder:
                return new("Tabf", new[] { "Tabf" }, bars =>
                {
                    var level = Average(Closes(bars), length, kind);
                    double[] Ratio(bool rising)
                    {
                        var selected = level.Select((v, i) => (rising ? v > (i == 0 ? 0 : level[i - 1]) : v < (i == 0 ? 0 : level[i - 1])) ? v : 0).ToArray();
                        return level.Select((v, i) =>
                        {
                            var sample = Window(selected, i, length).ToArray(); var mean = sample.Average();
                            var sigma = i + 1 < length ? 0 : Math.Sqrt(sample.Average(x => (x - mean) * (x - mean)));
                            return v + sigma == 0 ? 0 : v / (v + sigma);
                        }).ToArray();
                    }
                    var up = Ratio(true); var down = Ratio(false);
                    return Outputs(("Tabf", level.Select((_, i) => i == 0 ? 0d : up[i - 1] == 1 && up[i] != 1 ? 1 // NOSONAR: S1244 - The event contract detects departure from the exact endpoint.
                        : down[i - 1] == 1 && down[i] != 1 ? -1 : 0).ToArray())); // NOSONAR: S1244 - The event contract detects departure from the exact endpoint.
                });
            case IndicatorName.TTMScalperIndicator:
                return new("Sbs", new[] { "Sbs" }, bars =>
                {
                    double Price(int i) => i < 0 ? 0 : bars[i].Close;
                    var directions = bars.Select((b, i) => Price(i - 1) < b.Close && Math.Min(Price(i - 2), Price(i - 3)) < Price(i - 1) ? 1
                        : Price(i - 1) > b.Close && Math.Max(Price(i - 2), Price(i - 3)) > Price(i - 1) ? -1 : 0).ToArray();
                    var events = new List<(int Index, int Side)>();
                    for (var i = 0; i < directions.Length; i++)
                        if (directions[i] != 0 && directions[i] != (events.Count == 0 ? -1 : events[events.Count - 1].Side))
                            events.Add((i, directions[i]));
                    return Outputs(("Sbs", bars.Select((_, i) =>
                    {
                        var prior = events.Where(e => e.Index <= i).ToArray();
                        if (prior.Length == 0) return 0d;
                        var last = prior[prior.Length - 1];
                        return last.Side > 0 ? bars[last.Index].High : bars[last.Index].Low;
                    }).ToArray()));
                });
            case IndicatorName.HybridConvolutionFilter:
                return new("Hcf", new[] { "Hcf" }, bars =>
                {
                    var prices = Closes(bars);
                    var positions = Enumerable.Range(0, length + 1).Select(j => Math.Pow(Math.Sin(Math.PI * j / (2 * length)), 2)).ToArray();
                    var increments = Enumerable.Range(0, length).Select(j => positions[j + 1] - positions[j]).ToArray();
                    // Sum(s_j * delta_j) = (1 + sum(delta_j^2))/2 by telescoping squares.
                    var pole = (1 + increments.Sum(d => d * d)) / 2;
                    var drive = prices.Select((_, i) => Enumerable.Range(0, Math.Min(length, i + 1))
                        .Sum(lag => (1 - positions[lag + 1]) * increments[lag] * prices[i - lag])).ToArray();
                    return Outputs(("Hcf", prices.Select((_, i) => Math.Pow(pole, i + 1) * prices[0]
                        + Enumerable.Range(0, i + 1).Sum(j => Math.Pow(pole, i - j) * drive[j])).ToArray()));
                });
            case IndicatorName._1LCLeastSquaresMovingAverage:
                return new("1lsma", new[] { "1lsma" }, bars => OneLcValues(bars, length, kind).Outputs);
            case IndicatorName.JmaRsxClone:
                return new("Rsx", new[] { "Rsx" }, bars => RsxOutputs(bars, indicator));
            case IndicatorName.JurikMovingAverage:
                return new("Jma", new[] { "Jma" }, bars =>
                {
                    var prices = Closes(bars); var ratio = .45 * (length - 1);
                    var beta = ratio / (ratio + 2); var alpha = beta * beta;
                    double[] Filter(double[] values, double pole) => values.Select((_, i) => Enumerable.Range(0, i + 1)
                        .Sum(j => (1 - pole) * Math.Pow(pole, i - j) * values[j])).ToArray();
                    var level = Filter(prices, alpha);
                    var correction = Filter(prices.Select((v, i) => v - level[i]).ToArray(), beta);
                    var target = level.Select((v, i) => v + 2 * correction[i]).ToArray();
                    return Outputs(("Jma", prices.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j =>
                        (1 - alpha) * (1 - alpha) * (i - j + 1) * Math.Pow(alpha, i - j) * target[j])).ToArray()));
                });
            case IndicatorName.GrandTrendForecasting:
                return new("Gtf", new[] { "Gtf", "UpperBand", "MiddleBand", "LowerBand" }, bars => GrandForecastOutputs(bars, indicator));
            case IndicatorName.VanillaABCDPattern:
                return new("Vabcd", new[] { "Vabcd" }, bars =>
                {
                    double Price(int i) => i < 0 ? 0 : bars[i].Close;
                    var events = bars.Select((b, i) => Price(i - 3) > Price(i - 2) && Price(i - 1) > Price(i - 2) && b.Close < Price(i - 2) ? 1
                        : Price(i - 3) < Price(i - 2) && Price(i - 1) < Price(i - 2) && b.Close > Price(i - 2) ? -1 : 0).ToArray();
                    var regimes = events.Select((_, i) => events.Take(i + 1).LastOrDefault(v => v != 0) == 1 ? 1d : 0).ToArray();
                    return Outputs(("Vabcd", regimes.Select((v, i) => v - (i == 0 ? 0 : regimes[i - 1])).ToArray()));
                });
            case IndicatorName.TStepLeastSquaresMovingAverage:
                return new("Tslsma", new[] { "Tslsma" }, bars =>
                {
                    var prices = Closes(bars); var efficiency = EfficiencyRatios(bars, length);
                    var steps = new double[bars.Count]; var distances = new double[bars.Count];
                    for (var i = 0; i < steps.Length; i++)
                    {
                        var previous = i == 0 ? prices[i] : steps[i - 1];
                        distances[i] = Math.Abs(prices[i] - previous);
                        var threshold = distances.Take(i + 1).Average() * (2 - efficiency[i]);
                        // The step changes only outside the closed price interval. Comparing a
                        // subtracted distance instead changes rounding at an exact boundary.
                        var lower = previous - threshold; var upper = previous + threshold;
                        steps[i] = prices[i] < lower || prices[i] > upper ? prices[i] : previous;
                    }
                    var mean = Average(prices, length, kind); var stepMean = Average(steps, length, kind);
                    return Outputs(("Tslsma", prices.Select((_, i) =>
                    {
                        if (i + 1 < length) return mean[i];
                        var x = Window(steps, i, length).ToArray(); var y = Window(prices, i, length).ToArray();
                        var xm = x.Average(); var ym = y.Average();
                        var variance = x.Sum(v => (v - xm) * (v - xm));
                        var slope = variance == 0 ? 0 : x.Select((v, j) => (v - xm) * (y[j] - ym)).Sum() / variance;
                        return mean[i] + slope * (steps[i] - stepMean[i]);
                    }).ToArray()));
                });
            case IndicatorName.EhlersMedianAverageAdaptiveFilter:
                return new("Maaf", new[] { "Maaf" }, bars => MedianAdaptiveOutputs(bars, indicator));
            case IndicatorName.ElderSafeZoneStops:
                return new("Eszs", new[] { "Eszs" }, bars => Outputs(("Eszs", ElderSafeZoneOutputs(bars, indicator))));
            case IndicatorName.LiquidRelativeStrengthIndex:
                return new("Lrsi", new[] { "Lrsi" }, bars => LiquidRsiOutputs(bars, indicator));
            case IndicatorName.FisherLeastSquaresMovingAverage:
                return new("Flsma", new[] { "Flsma" }, bars =>
                {
                    var prices = Closes(bars); var indices = prices.Select((_, i) => (double)i).ToArray();
                    var mean = Average(prices, length, kind); var indexMean = Average(indices, length, kind);
                    var variance = PopulationVariance(prices, length); var indexVariance = PopulationVariance(indices, length);
                    var residuals = new double[bars.Count]; var estimates = new double[bars.Count];
                    for (var i = 0; i < estimates.Length; i++)
                    {
                        residuals[i] = prices[i] - (i == 0 ? prices[i] : estimates[i - 1]);
                        var sample = Window(residuals, i, length).ToArray(); var total = sample.Sum(Math.Abs);
                        var position = total == 0 ? 0 : Math.Tanh(sample.Sum() / total);
                        estimates[i] = mean[i] + (indexVariance[i] == 0 ? 0
                            : (i - indexMean[i]) * Math.Sqrt(variance[i] / indexVariance[i]) * position);
                    }
                    return Outputs(("Flsma", estimates));
                });
            case IndicatorName.SuperTrend:
                return new("Trend", new[] { "Trend" }, bars => Outputs(("Trend", SuperTrendOutputs(bars, indicator))));
            case IndicatorName.FXSniperIndicator:
                return new("FXSniper", new[] { "FXSniper" }, bars => FxSniperOutputs(bars, indicator));
            case IndicatorName.LBRPaintBars:
                return new("Aatr", new[] { "UpperBand", "LowerBand", "Aatr" }, bars => LbrPaintOutputs(bars, indicator));
            case IndicatorName.PseudoPolynomialChannel:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var prices = Closes(bars); var projected = new double[bars.Count]; var morph = Number(options, .9, "Morph");
                    for (var i = length + 1; i < projected.Length; i++)
                    {
                        var x1 = i - length; var x2 = Math.Max(0, i - 2 * length);
                        var left = projected[x1]; var right = i < 2 * length ? prices[i] : projected[x2];
                        // Lagrange interpolation of the two delayed, price-blended ordinates.
                        var first = morph * left + (1 - morph) * prices[i];
                        var second = morph * right + (1 - morph) * prices[i];
                        projected[i] = (first * (i - x2) - second * (i - x1)) / (x1 - x2);
                    }
                    var center = Average(projected, length, kind);
                    var widths = center.Select((_, i) => i == 0 ? 0 : Enumerable.Range(0, i + 1).Sum(j => Math.Abs(prices[j] - center[j])) / i).ToArray();
                    return Outputs(("MiddleBand", center), ("UpperBand", center.Select((v, i) => v + widths[i]).ToArray()),
                        ("LowerBand", center.Select((v, i) => v - widths[i]).ToArray()));
                });
            case IndicatorName.VervoortVolatilityBands:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var first = Integer(options, "Length1", 8); var second = Integer(options, "Length2", 13);
                    var mean = Average(Closes(bars), first, kind); var doubleMean = Average(mean, first, kind);
                    var displacement = bars.Select((b, i) => b.Close >= (i == 0 ? 0 : bars[i - 1].Close)
                        ? b.Close - (i == 0 ? 0 : bars[i - 1].Low) : (i == 0 ? 0 : bars[i - 1].Close) - b.Low).ToArray();
                    var width = Average(displacement.Select((_, i) => Window(displacement, i, second).Average() * Number(options, 3.55, "DevMult")).ToArray(), first, kind);
                    return Outputs(("MiddleBand", mean.Select((_, i) => Window(mean, i, first).Average()).ToArray()), ("UpperBand", doubleMean.Select((v, i) => v + width[i]).ToArray()),
                        ("LowerBand", doubleMean.Select((v, i) => v - Number(options, .9, "LowBandMult") * width[i]).ToArray()));
                });
            case IndicatorName.MovingAverageAdaptiveFilter:
                return new("Maaf", new[] { "Maaf" }, bars => MovingAverageAdaptiveFilterOutputs(bars, indicator));
            case IndicatorName.SwamiStochastics:
                return new("Ss", new[] { "Ss" }, bars =>
                {
                    var window = Math.Max(1, Integer(options, "SlowLength", 48) - Integer(options, "FastLength", 12));
                    var numerator = bars.Select((b, i) => b.Close - Window(bars, i, window).Min(v => v.Low)).ToArray();
                    var denominator = bars.Select((_, i) => Window(bars, i, window).Max(v => v.High) - Window(bars, i, window).Min(v => v.Low)).ToArray();
                    double Smooth(double[] values, int i, double gain) => Enumerable.Range(0, i + 1).Sum(j => gain * Math.Pow(1 - gain, i - j) * values[j]);
                    var ratios = numerator.Select((_, i) => Smooth(denominator, i, .5) == 0 ? 0 : Smooth(numerator, i, .5) / Smooth(denominator, i, .5)).ToArray();
                    return Outputs(("Ss", ratios.Select((_, i) => Smooth(ratios, i, .2)).ToArray()));
                });
            case IndicatorName.RecursiveStochastic:
                return new("Rsto", new[] { "Rsto" }, bars =>
                {
                    var prices = Closes(bars); var blended = new double[bars.Count]; var result = new double[bars.Count];
                    var alpha = Number(options, .1, "Alpha");
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var sample = Window(prices, i, Math.Max(2, length)).ToArray();
                        var span = sample.Max() - sample.Min();
                        var position = span == 0 ? 0 : (prices[i] - sample.Min()) / span;
                        blended[i] = alpha * position + (1 - alpha) * (i == 0 ? 0 : result[i - 1]);
                        var history = Window(blended, i, length).ToArray();
                        var range = history.Max() - history.Min();
                        result[i] = range == 0 ? 0 : Math.Max(0, Math.Min(1, (blended[i] - history.Min()) / range));
                    }
                    return Outputs(("Rsto", result.Select(v => 100 * v).ToArray()));
                });
            case IndicatorName.FisherTransformStochasticOscillator:
                return new("Ftso", new[] { "Ftso" }, bars =>
                {
                    var layer = Closes(bars); var rainbow = new double[bars.Count];
                    for (var depth = 0; depth < 10; depth++)
                    {
                        layer = Average(layer, length, 2);
                        for (var i = 0; i < layer.Length; i++) rainbow[i] += Math.Max(1, 5 - depth) * layer[i] / 20;
                    }
                    var lows = rainbow.Select((_, i) => Window(rainbow, i, 30).Min()).ToArray();
                    var ranges = rainbow.Select((v, i) => Window(rainbow, i, 30).Max() - lows[i]).ToArray();
                    var distances = rainbow.Select((v, i) => v - lows[i]).ToArray();
                    return Outputs(("Ftso", rainbow.Select((_, i) =>
                    {
                        var position = Window(distances, i, 5).Sum() / (Window(ranges, i, 5).Sum() + .0001);
                        return 100 / (1 + Math.Exp(10 - 20 * Math.Max(0, Math.Min(1, position))));
                    }).ToArray()));
                });
            case IndicatorName.MorphedSineWave:
                return new("Msw", new[] { "Msw" }, bars => MorphedSineOutputs(bars, indicator));
            case IndicatorName.MultiDepthZeroLagExponentialMovingAverage:
                return new("Md2Pole", new[] { "Md2Pole", "Md1Pole", "Md3Pole" }, bars =>
                {
                    var radius2 = Math.Exp(-Math.Sqrt(2) * Math.PI / length);
                    var radius3 = Math.Exp(-Math.PI / length);
                    var b = 2 * radius3 * Math.Cos(Math.Sqrt(3) * Math.PI / length);
                    var c = radius3 * radius3;
                    var prices = Closes(bars);
                    return Outputs(("Md1Pole", DepthCorrectionReference(prices, new[] { 1 - 2d / (length + 1) })),
                        ("Md2Pole", DepthCorrectionReference(prices, new[] { 2 * radius2 * Math.Cos(Math.Sqrt(2) * Math.PI / length), -radius2 * radius2 })),
                        ("Md3Pole", DepthCorrectionReference(prices, new[] { b + c, -c * (1 + b), c * c })));
                });
            case IndicatorName.KalmanSmoother:
                return new("Ks", new[] { "Ks" }, bars =>
                {
                    var q = length / 10000d; var g = Math.Sqrt(2 * q);
                    var impulse = SecondOrderImpulse(bars.Count, 2 - g - q, g - 1);
                    var prices = Closes(bars);
                    return Outputs(("Ks", prices.Select((_, i) => prices[0] + Enumerable.Range(1, i).Sum(j =>
                        (prices[j] - prices[0]) * ((g + q) * impulse[i - j] - (i > j ? g * impulse[i - j - 1] : 0)))).ToArray()));
                });
            case IndicatorName.IIRLeastSquaresEstimate:
                return new("IIRLse", new[] { "IIRLse" }, bars =>
                {
                    var a = 4d / (length + 2);
                    var half = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d)));
                    var k = Math.Max(.01, Math.Min(.99, 2d / (half + 1)));
                    var impulse = SecondOrderImpulse(bars.Count, 2 - k - a * k, k - 1);
                    var prices = Closes(bars);
                    // Eliminate the internal EMA: numerator a(1-(1-k)z^-1), plus the seeded estimate.
                    return Outputs(("IIRLse", prices.Select((_, i) =>
                        Enumerable.Range(0, i + 1).Sum(j => a * prices[j] * (impulse[i - j] - (i > j ? (1 - k) * impulse[i - j - 1] : 0)))
                        + prices[0] * ((1 - a * k) * impulse[i] - (i > 0 ? (1 - k) * impulse[i - 1] : 0))).ToArray()));
                });
            case IndicatorName.ChandeVolatilityIndexDynamicAverageIndicator:
            case IndicatorName.VolatilityIndexDynamicAverageIndicator:
                var prefix = name == IndicatorName.ChandeVolatilityIndexDynamicAverageIndicator ? "Cvida" : "Vida";
                return new(prefix + "1", new[] { prefix + "1", prefix + "2" }, bars => VolatilityIndexOutputs(bars, indicator));
            case IndicatorName.UhlMaCrossoverSystem:
                return new("Cts", new[] { "Cts", "Cma" }, bars =>
                {
                    var prices = Closes(bars);
                    var mean = Average(prices, length, kind);
                    var variance = PopulationVariance(prices, length);
                    double[] Follow(double[] values)
                    {
                        var line = new double[values.Length];
                        for (var i = 0; i < line.Length; i++)
                        {
                            var previous = i == 0 ? prices[i] : line[i - 1];
                            var distance = values[i] - previous;
                            var noise = i < length ? 0 : variance[i - length];
                            line[i] = distance * distance > noise ? values[i] - noise / distance : previous;
                        }
                        return line;
                    }
                    return Outputs(("Cts", Follow(prices)), ("Cma", Follow(mean)));
                });
            case IndicatorName.GeneralFilterEstimator:
                return new("Gfe", new[] { "Gfe" }, bars =>
                {
                    // With the exposed defaults gamma=zeta=1, both estimator stages coincide.
                    // Solve the delayed-feedback filter through its impulse response.
                    var delay = (int)Math.Ceiling(length / 5.25);
                    var impulse = new double[bars.Count];
                    for (var i = 0; i < impulse.Length; i++)
                        impulse[i] = i == 0 ? 1d / delay : impulse[i - 1] - (i >= delay ? impulse[i - delay] / delay : 0);
                    var values = bars.Select((b, i) => b.Close).ToArray();
                    return Outputs(("Gfe", values.Select((_, i) => values[0] + Enumerable.Range(delay, Math.Max(0, i - delay + 1))
                        .Sum(j => impulse[i - j] * (values[j] - values[0]))).ToArray()));
                });
            case IndicatorName.GroverLlorensActivator:
            case IndicatorName.GroverLlorensCycleOscillator:
                var cycle = name == IndicatorName.GroverLlorensCycleOscillator;
                var key = cycle ? "Glco" : "Gla";
                return new(key, new[] { key }, bars =>
                {
                    var atr = Average(TrueRanges(bars), length, kind);
                    var trail = new double[bars.Count];
                    var multiplier = Number(options, cycle ? 10 : 5, "Mult");
                    for (var i = 0; i < trail.Length; i++)
                    {
                        var previous = i == 0 ? bars[i].Close : trail[i - 1];
                        if (!cycle && previous == 0) previous = i == 0 ? 0 : bars[i - 1].Close;
                        trail[i] = previous - Math.Sign(bars[i].Close - previous) * multiplier * atr[i];
                    }
                    return Outputs((key, cycle ? MotionRsi(Average(bars.Select((b, i) => b.Close - trail[i]).ToArray(), 20, kind), 20, kind) : trail));
                });
            case IndicatorName.MovingAverageAdaptiveQ:
                return new("Maaq", new[] { "Maaq" }, bars => MovingAverageAdaptiveQOutputs(bars, indicator));
            case IndicatorName.OscarIndicator:
                return new("Oscar", new[] { "Oscar" }, bars =>
                {
                    var positions = bars.Select((b, i) =>
                    {
                        var sample = Window(bars, i, length).ToArray();
                        var low = sample.Min(v => v.Low); var high = sample.Max(v => v.High);
                        return high == low ? 0 : Math.Max(0, Math.Min(100, 100 * (b.Close - low) / (high - low))); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }).ToArray();
                    return Outputs(("Oscar", positions.Select((_, i) => Enumerable.Range(0, i + 1)
                        .Sum(j => positions[j] * Math.Pow(1d / 6, i - j) / 3)).ToArray()));
                });
            case IndicatorName.KarobeinOscillator:
                return new("Ko", new[] { "Ko" }, bars =>
                {
                    var mean = Average(Closes(bars), length, kind);
                    var ratio = mean.Select((v, i) => i == 0 || mean[i - 1] == 0 ? 0 : v / mean[i - 1]).ToArray();
                    var falls = Average(ratio.Select((v, i) => i > 0 && mean[i] < mean[i - 1] ? v : 0).ToArray(), length, kind);
                    var rises = Average(ratio.Select((v, i) => i > 0 && mean[i] > mean[i - 1] ? v : 0).ToArray(), length, kind);
                    return Outputs(("Ko", ratio.Select((v, i) =>
                    {
                        if (v == 0) return 0d;
                        var c = Math.Max(0, Math.Min(1, v / (v + rises[i])));
                        return Math.Max(0, Math.Min(1, (v - c * falls[i]) / (v + c * falls[i])));
                    }).ToArray()));
                });
            case IndicatorName.ModularFilter:
                return new("Mf", new[] { "Mf" }, bars =>
                {
                    var values = Closes(bars);
                    var result = new double[values.Length];
                    var alpha = 2d / (length + 1);
                    var beta = Number(options, .8, "Beta");
                    double high = 0, low = 0; var upperSide = true;
                    for (var i = 0; i < values.Length; i++)
                    {
                        if (i == 0) { high = low = values[i]; result[i] = values[i]; continue; }
                        high = Math.Max(values[i], high + alpha * (values[i] - high));
                        low = Math.Min(values[i], low + alpha * (values[i] - low));
                        if (values[i] >= high) upperSide = true;
                        else if (values[i] <= low) upperSide = false;
                        var weight = upperSide ? beta : 1 - beta;
                        result[i] = low + weight * (high - low);
                    }
                    return Outputs(("Mf", result));
                });
            case IndicatorName.TillsonIE2:
                return new("Ie2", new[] { "Ie2" }, bars =>
                {
                    var values = Closes(bars);
                    var fit = RegressionEndpoints(values, length);
                    var mean = Average(values, length, kind);
                    return Outputs(("Ie2", fit.Select((v, i) => v + (mean[i] - (i == 0 ? 0 : fit[i - 1])) / 2).ToArray()));
                });
            case IndicatorName.TheRangeIndicator:
                return new("Tri", new[] { "Tri" }, bars =>
                {
                    var ranges = TrueRanges(bars);
                    var values = ranges.Select((v, i) => i > 0 && bars[i].Close > bars[i - 1].Close ? v / (bars[i].Close - bars[i - 1].Close) : v).ToArray();
                    var positions = values.Select((v, i) =>
                    {
                        var sample = Window(values, i, length).ToArray();
                        var span = sample.Max() - sample.Min();
                        return span == 0 ? 0 : Math.Max(0, Math.Min(100, 100 * (v - sample.Min()) / span));
                    }).ToArray();
                    return Outputs(("Tri", Average(positions, length, kind)));
                });
            case IndicatorName.TradingMadeMoreSimplerOscillator:
                return new("Tmmso", new[] { "Tmmso" }, bars =>
                {
                    // This options type exposes only the RSI/second-stochastic period; other settings retain published defaults.
                    var strength = MotionRsi(Closes(bars), length, 2);
                    double[] Stochastic(int period) => Average(bars.Select((b, i) =>
                    {
                        var window = Window(bars, i, period).ToArray();
                        var low = window.Min(v => v.Low);
                        var high = window.Max(v => v.High);
                        return high == low ? 0 : Math.Max(0, Math.Min(100, 100 * (b.Close - low) / (high - low))); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }).ToArray(), 3, 2);
                    var fast = Stochastic(8);
                    var slow = Stochastic(length);
                    return Outputs(("Tmmso", strength.Select((v, i) => v > 50 && fast[i] > 50 && slow[i] > 50 ? slow[i] - 50
                        : v < 50 && fast[i] < 50 && slow[i] < 50 ? 50 - slow[i] : 0).ToArray()));
                });
            case IndicatorName.TurboTrigger:
                return new("BullLine", new[] { "BullLine", "Trigger" }, bars =>
                {
                    var close = Average(Closes(bars), 2, kind);
                    var open = Average(bars.Select(b => b.Open).ToArray(), 2, kind);
                    var high = Average(bars.Select(b => b.High).ToArray(), 2, kind);
                    var low = Average(bars.Select(b => b.Low).ToArray(), 2, kind);
                    var center = Average(close.Select((v, i) => (v + open[i]) / 2).ToArray(), length, kind);
                    var bull = Average(high.Select((v, i) => v - center[i]).ToArray(), length, kind);
                    var bear = Average(low.Select((v, i) => center[i] - v).ToArray(), length, kind);
                    return Outputs(("BullLine", bull), ("Trigger", Average(bull.Select((v, i) => v - bear[i]).ToArray(), length, kind)));
                });
            case IndicatorName.RetrospectiveCandlestickChart:
                return new("Rcc", new[] { "Rcc" }, bars =>
                {
                    var prices = Closes(bars);
                    var changes = prices.Select((v, i) => Math.Abs(v - (i == 0 ? 0 : prices[i - 1]))).ToArray();
                    var weights = changes.Select((v, i) =>
                    {
                        var sample = Window(changes, i, length).ToArray();
                        return sample.Max() == sample.Min() ? 0 : (v - sample.Min()) / (sample.Max() - sample.Min()); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }).ToArray();
                    var closes = ExpandedGainTrajectory(prices, weights);
                    var result = bars.Select((b, i) => i == 0 ? (b.Open + b.High + b.Low + b.Close) / 4
                        : closes[i - 1] + weights[i] * ((b.Open + b.High + b.Low + b.Close) / 4 - closes[i - 1])).ToArray();
                    return Outputs(("Rcc", result));
                });
            case IndicatorName.TrendImpulseFilter:
                return new("Tif", new[] { "Tif" }, bars =>
                {
                    var prices = Closes(bars);
                    var held = new double[bars.Count];
                    var lookback = Math.Max(2, Integer(options, "Length1", 100));
                    for (var i = 0; i < held.Length; i++)
                    {
                        if (i == 0) { held[i] = prices[i]; continue; }
                        var previous = Window(prices, i - 1, lookback).ToArray();
                        held[i] = prices[i] > previous.Max() || prices[i] < previous.Min() ? prices[i] : held[i - 1];
                    }
                    return Outputs(("Tif", Average(held, Integer(options, "Length2", 10), kind)));
                });
            case IndicatorName.SettingLessTrendStepFiltering:
                return new("Sltsf", new[] { "Sltsf" }, bars =>
                {
                    var result = new double[bars.Count];
                    var distances = new List<double>();
                    double threshold = 0;
                    for (var i = 0; i < result.Length; i++)
                    {
                        var previous = i == 0 ? bars[i].Close : result[i - 1];
                        var distance = Math.Abs(bars[i].Close - previous);
                        var gain = distance + threshold == 0 ? 0 : distance / (distance + threshold);
                        var step = gain * (bars[i].Close - previous);
                        distances.Add(Math.Abs(step));
                        threshold = distances.Average() * (1 + gain);
                        result[i] = Math.Abs(step) > threshold ? previous + step : previous;
                    }
                    return Outputs(("Sltsf", result));
                });
            case IndicatorName.TradersDynamicIndex:
                return new("Tdi", new[] { "UpperBand", "MiddleBand", "LowerBand", "Tdi", "Signal" }, bars =>
                {
                    var strength = MotionRsi(Closes(bars), Integer(options, "Length1", 13), kind);
                    var bandPeriod = Integer(options, "Length2", 34);
                    var center = Average(strength, bandPeriod, kind);
                    var widths = strength.Select((_, i) =>
                    {
                        if (i + 1 < bandPeriod) return 0d;
                        var sample = Window(strength, i, bandPeriod).ToArray();
                        var mean = sample.Average();
                        return 1.6185 * Math.Sqrt(sample.Sum(v => (v - mean) * (v - mean)) / bandPeriod);
                    }).ToArray();
                    return Outputs(("UpperBand", center.Select((v, i) => v + widths[i]).ToArray()), ("MiddleBand", center),
                        ("LowerBand", center.Select((v, i) => v - widths[i]).ToArray()),
                        ("Tdi", Average(strength, Integer(options, "Length3", 2), kind)),
                        ("Signal", Average(strength, Integer(options, "Length4", 7), kind)));
                });
            case IndicatorName.RahulMohindarOscillator:
                return new("Rmo", new[] { "Rmo", "SwingTrade1", "SwingTrade2", "SwingTrade3" }, bars =>
                {
                    var prices = Closes(bars);
                    var cascades = new List<double[]> { Average(prices, 2, 1) };
                    for (var stage = 1; stage < 10; stage++) cascades.Add(Average(cascades[stage - 1], 2, 1));
                    var swing = prices.Select((v, i) =>
                    {
                        var sample = Window(prices, i, Math.Max(2, length)).ToArray();
                        var range = sample.Max() - sample.Min();
                        return range == 0 ? 0 : 100 * (v - cascades.Sum(stage => stage[i]) / 10) / range;
                    }).ToArray();
                    var second = Average(swing, 30, 3);
                    return Outputs(("Rmo", Average(swing, 81, 3)), ("SwingTrade1", swing),
                        ("SwingTrade2", second), ("SwingTrade3", Average(second, 30, 3)));
                });
            case IndicatorName.ReverseEngineeringRelativeStrengthIndex:
                return new("Rersi", new[] { "Rersi" }, bars =>
                {
                    var target = Number(options, 50, "RsiLevel") / 100;
                    var retention = 1 - 1d / length;
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        // Seeded gain/loss histories expanded as geometric observation weights.
                        var gains = Math.Pow(retention, i + 1);
                        var losses = gains;
                        for (var j = 1; j <= i; j++)
                        {
                            var weight = Math.Pow(retention, i - j) / length;
                            gains += weight * Math.Max(0, changes[j]);
                            losses += weight * Math.Max(0, -changes[j]);
                        }
                        // Solve the next Wilder gain/loss ratio for the requested RSI level.
                        var displacement = (length - 1) * (target * losses - (1 - target) * gains);
                        result[i] = bars[i].Close + displacement / (displacement >= 0 ? 1 - target : target);
                    }
                    return Outputs(("Rersi", result));
                });
            case IndicatorName.HistoricalVolatilityPercentile:
                return new("Hvp", new[] { "Hvp", "Signal" }, bars =>
                {
                    var annual = Integer(options, "AnnualLength", 252);
                    var logs = bars.Select((b, i) => i > 0 && bars[i - 1].Close != 0 && b.Close != 0
                        && Math.Sign(b.Close) == Math.Sign(bars[i - 1].Close)
                        ? (ReferenceFraction.FromDouble(Math.Abs(b.Close)) / ReferenceFraction.FromDouble(Math.Abs(bars[i - 1].Close))).LogToDouble() : 0).ToArray();
                    // Published residual volatility uses each log return's contemporaneous window mean.
                    var residuals = logs.Select((v, i) => Math.Pow(v - Window(logs, i, length).Average(), 2)).ToArray();
                    var volatility = logs.Select((_, i) => length == 1 ? 0 : Math.Sqrt(Window(residuals, i, length).Sum() * annual / (length - 1))).ToArray();
                    var rank = volatility.Select((v, i) => 100d * Window(volatility, i, annual).Count(prior => prior < v - Math.Abs(v) * 32 * Math.Pow(2, -52)) / annual).ToArray();
                    return Outputs(("Hvp", rank), ("Signal", Average(rank, length, kind)));
                });
            case IndicatorName.KaufmanBinaryWave:
                return new("Kbw", new[] { "Kbw" }, bars =>
                {
                    var gains = EfficiencyRatios(bars, length).Select(er => Math.Pow(er * Number(options, .6022, "FastSc") + Number(options, .0645, "SlowSc"), 2)).ToArray();
                    var average = ExpandedGainTrajectory(Closes(bars), gains);
                    var changes = average.Select((v, i) => i == 0 ? 0 : v - average[i - 1]).ToArray();
                    var result = new double[bars.Count];
                    double lastFall = 0, lastRise = 0;
                    for (var i = 0; i < result.Length; i++)
                    {
                        var sample = Window(changes, i, length).ToArray();
                        var mean = sample.Average();
                        var deviation = i + 1 < length ? 0 : Math.Sqrt(sample.Sum(v => (v - mean) * (v - mean)) / length);
                        var threshold = Number(options, 10, "FilterPct") * deviation / 100;
                        if (changes[i] < 0) lastFall = average[i];
                        if (changes[i] > 0) lastRise = average[i];
                        result[i] = average[i] - lastFall > threshold ? 1 : lastRise - average[i] > threshold ? -1 : 0;
                    }
                    return Outputs(("Kbw", result));
                });
            case IndicatorName.QuantitativeQualitativeEstimation:
                return new("FastAtrRsi", new[] { "FastAtrRsi", "SlowAtrRsi" }, bars =>
                {
                    var oscillator = Average(MotionRsi(Closes(bars), length, kind), Integer(options, "SmoothLength", 5), kind);
                    var movements = oscillator.Select((v, i) => Math.Abs(v - (i == 0 ? 0 : oscillator[i - 1]))).ToArray();
                    var width = Average(Average(movements, 2 * length - 1, kind), 2 * length - 1, kind);
                    return Outputs(("FastAtrRsi", width.Select(v => v * Number(options, 2.618, "FastFactor")).ToArray()),
                        ("SlowAtrRsi", width.Select(v => v * Number(options, 4.236, "SlowFactor")).ToArray()));
                });
            case IndicatorName.PrimeNumberOscillator:
                return new("Pno", new[] { "Pno" }, bars => Outputs(("Pno", PrimeOffsetsReference(Closes(bars), length))));
            case IndicatorName.PrimeNumberBands:
                return new("UpperBand", new[] { "UpperBand", "LowerBand" }, bars =>
                {
                    var high = PrimeOffsetsReference(bars.Select(b => b.High).ToArray(), length);
                    var low = PrimeOffsetsReference(bars.Select(b => b.Low).ToArray(), length);
                    return Outputs(("UpperBand", high.Select((_, i) => Window(high, i, Math.Max(2, length)).Max()).ToArray()),
                        ("LowerBand", low.Select((_, i) => Window(low, i, Math.Max(2, length)).Min()).ToArray()));
                });
            case IndicatorName.SuperTrendFilter:
                return new("Stf", new[] { "Stf" }, bars => Outputs(("Stf", SuperTrendFilterOutputs(bars, indicator))));
            case IndicatorName.VolatilityStop:
                var stopFactor = Number(options, 2, "Multiplier");
                return new("Vs", new[] { "Vs" }, bars => VolatilityStopValues(bars, length, stopFactor).Outputs);
            case IndicatorName.VixTradingSystem:
                return new("Vix", new[] { "Vix" }, bars =>
                {
                    var means = Average(Closes(bars), length, kind);
                    if (bars.Count == 0) return Outputs(("Vix", Array.Empty<double>()));
                    var above = bars.Select((b, i) => b.Close > means[i]).ToArray();
                    // The published counter keeps the direction chosen at the opening sample.
                    return Outputs(("Vix", above.Select((_, i) => (above[0] ? 1d : -1d)
                        * above.Take(i + 1).Count(v => v == above[0])).ToArray()));
                });
            case IndicatorName.VostroIndicator:
                return new("Vi", new[] { "Vi" }, bars =>
                {
                    var period = Integer(options, "Length1", 5);
                    var average = Average(Closes(bars), Integer(options, "Length2", 100), kind);
                    var level = Number(options, 8, "Level");
                    var lower = new double[bars.Count]; var upper = new double[bars.Count];
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var samples = Window(bars, i, period).ToArray();
                        // The definition fixes these weights at one fifth and one twenty-fifth.
                        var center = samples.Sum(b => b.Close) / 5;
                        var scale = samples.Sum(b => b.High - b.Low) / 25;
                        lower[i] = scale == 0 ? 0 : (bars[i].Low - center) / scale;
                        upper[i] = scale == 0 ? 0 : (bars[i].High - center) / scale;
                    }
                    return Outputs(("Vi", bars.Select((bar, i) =>
                    {
                        if (i > 0 && ((upper[i] > level && upper[i - 1] > level) || (lower[i] < -level && lower[i - 1] < -level))) return 0d;
                        return upper[i] > level && bar.High > average[i] ? 90d : lower[i] < -level && bar.Low < average[i] ? -90d : 0d;
                    }).ToArray()));
                });
            case IndicatorName.DynamicMomentumOscillator:
                return new("Dmo", new[] { "Dmo" }, bars =>
                {
                    var rank = bars.Select((bar, i) =>
                    {
                        var sample = Window(bars, i, length).ToArray();
                        var low = sample.Min(b => b.Low); var high = sample.Max(b => b.High);
                        return high == low ? 0 : Clamp(100 * (bar.Close - low) / (high - low), 0, 100); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }).ToArray();
                    var fast = Average(rank, length, kind);
                    var slow = Average(fast, 20, kind);
                    return Outputs(("Dmo", fast.Select((v, i) => Clamp(
                        (fast.Take(i + 1).Min() + Math.Max(0, fast.Take(i + 1).Max())) / 2 + v - slow[i], 0, 100)).ToArray()));
                });
            case IndicatorName.DirectionalTrendIndex:
                if (kind is 1 or 2 or 3 or 6) return new("Dti", new[] { "Dti" }, bars => DirectionalStrengthOutputs(bars, indicator));
                return new("Dti", new[] { "Dti" }, bars =>
                {
                    var changes = bars.Select((b, i) => Math.Max(0, b.High - (i == 0 ? 0 : bars[i - 1].High))
                        - Math.Max(0, (i == 0 ? 0 : bars[i - 1].Low) - b.Low)).ToArray();
                    double[] Triple(double[] source) => Average(Average(Average(source, length, kind), 10, kind), 5, kind);
                    var signed = Triple(changes); var absolute = Triple(changes.Select(Math.Abs).ToArray());
                    return Outputs(("Dti", signed.Select((v, i) => absolute[i] == 0 ? 0 : Clamp(100 * v / absolute[i], -100, 100)).ToArray()));
                });
            case IndicatorName.CommoditySelectionIndex:
                return new("Csi", new[] { "Csi", "Signal" }, bars => CommoditySelectionOutputs(bars, indicator));
            case IndicatorName.ErgodicCommoditySelectionIndex:
                return new("Ecsi", new[] { "Ecsi", "Signal" }, bars => ErgodicSelectionOutputs(bars, indicator));
            case IndicatorName.DMIStochastic:
                if (name == IndicatorName.DMIStochastic && kind is 1 or 2 or 3 or 6)
                    return new("DmiStochastic", new[] { "DmiStochastic" }, bars => DmiStochasticOutputs(bars, length, kind));
                var ergodicCommodity = name == IndicatorName.ErgodicCommoditySelectionIndex;
                var commodity = name != IndicatorName.DMIStochastic;
                var commodityKey = ergodicCommodity ? "Ecsi" : "Csi";
                return new(commodity ? commodityKey : "DmiStochastic", commodity ? new[] { commodityKey, "Signal" } : new[] { "DmiStochastic" }, bars =>
                {
                    var tr = Average(TrueRanges(bars), length, kind);
                    var upward = bars.Select((b, i) => i == 0 ? 0 : Math.Max(0, b.High - bars[i - 1].High)).ToArray();
                    var downward = bars.Select((b, i) => i == 0 ? 0 : Math.Max(0, bars[i - 1].Low - b.Low)).ToArray();
                    var plus = Average(upward.Select((v, i) => v > downward[i] ? v : 0).ToArray(), length, kind)
                        .Select((v, i) => tr[i] == 0 ? 0 : Clamp(100 * v / tr[i], 0, 100)).ToArray();
                    var minus = Average(downward.Select((v, i) => v > upward[i] ? v : 0).ToArray(), length, kind)
                        .Select((v, i) => tr[i] == 0 ? 0 : Clamp(100 * v / tr[i], 0, 100)).ToArray();
                    if (commodity)
                    {
                        var dx = plus.Select((v, i) => v + minus[i] == 0 ? 0 : 100 * Math.Abs(v - minus[i]) / (v + minus[i])).ToArray();
                        var strength = Average(dx, length, kind);
                        if (ergodicCommodity)
                        {
                            var smoothLength = Integer(options, "SmoothLength", 5);
                            var scale = 100 * Number(options, 1, "PointValue") / (Math.Sqrt(length) * (150 + smoothLength) * length);
                            var raw = TrueRanges(bars).Select((v, i) => bars[i].Close <= 0 ? 0
                                : scale * (strength[i] + (i == 0 ? 0 : strength[i - 1])) / 2 * v / bars[i].Close).ToArray();
                            return Outputs(("Ecsi", raw), ("Signal", Average(raw, smoothLength, kind)));
                        }
                        var line = tr.Select((v, i) => 100 * 50 / (Math.Sqrt(3000) * 160) * v * strength[i]).ToArray();
                        return Outputs(("Csi", line), ("Signal", line.Select((_, i) => Window(line, i, length).Average()).ToArray()));
                    }
                    var difference = minus.Select((v, i) => v - plus[i]).ToArray();
                    var rank = difference.Select((v, i) =>
                    {
                        var window = Window(difference, i, 10).ToArray();
                        return window.Max() == window.Min() ? 0 : 100 * (v - window.Min()) / (window.Max() - window.Min()); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }).ToArray();
                    return Outputs(("DmiStochastic", Average(Average(rank, 3, kind), 3, kind)));
                });
            case IndicatorName.EhlersRelativeStrengthIndexInverseFisherTransform:
            case IndicatorName.EhlersCommodityChannelIndexInverseFisherTransform:
                if (name == IndicatorName.EhlersRelativeStrengthIndexInverseFisherTransform && kind is 1 or 2 or 3 or 6)
                    return new("Eiftrsi", new[] { "Eiftrsi" }, bars => RsiInverseFisherOutputs(bars, indicator));
                if (name == IndicatorName.EhlersCommodityChannelIndexInverseFisherTransform && kind is 1 or 2 or 3 or 6)
                    return new("Eiftcci", new[] { "Eiftcci" }, bars => CommodityOutputs(bars, indicator));
                var inverseCommodity = name == IndicatorName.EhlersCommodityChannelIndexInverseFisherTransform;
                var inverseKey = inverseCommodity ? "Eiftcci" : "Eiftrsi";
                return new(inverseKey, new[] { inverseKey }, bars =>
                {
                    var prices = bars.Select(b => (b.High + b.Low + b.Close) / 3).ToArray();
                    var constant = Number(options, .015, "Constant");
                    var source = !inverseCommodity ? MotionRsi(Closes(bars), length, kind) : kind != 1
                        ? SmoothedChannelIndex(prices, length, kind, constant)
                        : prices.Select((v, i) =>
                        {
                            if (i + 1 < length) return 0;
                            var window = Window(prices, i, length).ToArray();
                            var mean = window[0] + window.Average(x => x - window[0]);
                            var deviation = window.Average(x => Math.Abs(x - mean));
                            return deviation == 0 ? 0 : (v - mean) / (constant * deviation);
                        }).ToArray();
                    var smoothed = Average(source.Select(v => .1 * (v - 50)).ToArray(), Integer(options, "SignalLength", 9), kind);
                    return Outputs((inverseKey, smoothed.Select(Math.Tanh).ToArray()));
                });
            case IndicatorName.SupportAndResistanceOscillator:
                return new("Sro", new[] { "Sro" }, bars =>
                {
                    var range = TrueRanges(bars);
                    return Outputs(("Sro", bars.Select((b, i) => range[i] == 0 ? 0 :
                        Math.Max(0, Math.Min(1, (b.High - b.Open + b.Close - b.Low) / (2 * range[i])))).ToArray()));
                });
            case IndicatorName.UberTrendIndicator:
                return new("Uti", new[] { "Uti" }, bars =>
                {
                    var change = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var advance = change.Select((_, i) => Window(change, i, length).Sum(v => Math.Max(0, v))).ToArray();
                    var decline = change.Select((_, i) => Window(change, i, length).Sum(v => Math.Max(0, -v))).ToArray();
                    var upVolume = bars.Select((b, i) => change[i] <= 0 || advance[i] == 0 ? 0 : b.Volume / advance[i]).ToArray();
                    var downVolume = bars.Select((b, i) => change[i] >= 0 || decline[i] == 0 ? 0 : b.Volume / decline[i]).ToArray();
                    return Outputs(("Uti", bars.Select((_, i) =>
                    {
                        var up = Window(upVolume, i, length).Sum(); var down = Window(downVolume, i, length).Sum();
                        // Preserve the published zero-denominator convention, including all-rising windows.
                        var ratio = decline[i] == 0 || up == 0 || down == 0 ? 0 : advance[i] * down / (decline[i] * up);
                        return ratio == -1 ? 0 : (ratio - 1) / (ratio + 1); // NOSONAR: S1244 - Only minus one makes the following denominator exactly zero.
                    }).ToArray()));
                });
            case IndicatorName.KwanIndicator:
                return new("Ki", new[] { "Ki" }, bars => KwanOutputs(bars, indicator));
            case IndicatorName.KaseSerialDependencyIndex:
                return new("KsdiUp", new[] { "KsdiUp", "KsdiDn" }, bars =>
                {
                    var logs = bars.Select((b, i) => i == 0 ? 0 : ReferenceSameSignLogRatio(b.Close, bars[i - 1].Close)).ToArray();
                    var variance = PopulationVariance(logs, length);
                    double[] Side(bool up) => bars.Select((b, i) =>
                    {
                        if (i < length || variance[i] == 0) return 0;
                        var prior = up ? bars[i - length].Low : bars[i - length].High;
                        return ReferenceSameSignLogRatio(up ? b.High : b.Low, prior) / Math.Sqrt(variance[i]);
                    }).ToArray();
                    return Outputs(("KsdiUp", Side(true)), ("KsdiDn", Side(false)));
                });
            case IndicatorName.PriceCycleOscillator:
                return new("Pco", new[] { "Pco" }, bars =>
                {
                    var atr = Average(TrueRanges(bars), length, kind);
                    var distance = Average(bars.Select(b => b.Close - b.Low).ToArray(), length, kind);
                    return Outputs(("Pco", distance.Select((v, i) => atr[i] == 0 ? 0 : 100 * v / atr[i]).ToArray()));
                });
            case IndicatorName.PhaseChangeIndex:
                return new("Pci", new[] { "Pci", "Signal" }, bars =>
                {
                    var window = Math.Max(2, length);
                    var values = bars.Select((b, i) =>
                    {
                        var start = i < window ? 0 : bars[i - window].Close;
                        var change = i < window ? 0 : b.Close - start;
                        var residuals = Enumerable.Range(1, window).Select(lag =>
                            (i < lag ? 0 : bars[i - lag].Close) - start - change * lag / (window - 1)).ToArray();
                        var total = residuals.Sum(Math.Abs);
                        return total == 0 ? 0 : 100 * residuals.Sum(v => Math.Max(0, v)) / total;
                    }).ToArray();
                    return Outputs(("Pci", values), ("Signal", Average(values, Integer(options, "SmoothLength", 3), kind)));
                });
            case IndicatorName.KasePeakOscillatorV1:
                return new("Kpo", new[] { "Kpo", "Pk" }, bars =>
                {
                    var peak = KaseReferencePeak(bars, length, Integer(options, "SmoothLength", 3));
                    var mean = Average(peak, length, 1);
                    var variance = PopulationVariance(peak, length);
                    var levels = peak.Select((v, i) => v > 0 && (i == 0 || peak[i - 1] >= 0)
                        ? Math.Max(2.08, mean[i] + 1.33 * Math.Sqrt(variance[i]))
                        : v < 0 && (i == 0 || peak[i - 1] <= 0)
                            ? Math.Min(-1.92, mean[i] - 1.33 * Math.Sqrt(variance[i])) : 0).ToArray();
                    return Outputs(("Kpo", levels), ("Pk", peak));
                });
            case IndicatorName.KaseConvergenceDivergence:
                return new("Kcd", new[] { "Kcd" }, bars => KaseConvergenceOutputs(bars, indicator));
            case IndicatorName.KasePeakOscillatorV2:
                return new("Kpo", new[] { "Kpo" }, bars => KasePeakV2Outputs(bars, indicator));
            case IndicatorName.RandomWalkIndex:
                return new("RwiHigh", new[] { "RwiHigh", "RwiLow" }, bars =>
                {
                    var scale = Average(TrueRanges(bars), length, kind).Select(v => v * Math.Sqrt(length)).ToArray();
                    return Outputs(("RwiHigh", bars.Select((b, i) => scale[i] == 0 ? 0 :
                        (b.High - (i < length ? 0 : bars[i - length].Low)) / scale[i]).ToArray()),
                        ("RwiLow", bars.Select((b, i) => scale[i] == 0 ? 0 :
                        ((i < length ? 0 : bars[i - length].High) - b.Low) / scale[i]).ToArray()));
                });
            case IndicatorName.RunningEquity:
                return new("Req", new[] { "Req" }, bars =>
                {
                    var mean = Average(Closes(bars), length, kind);
                    var increments = bars.Select((b, i) => i == 0 ? 0 :
                        (b.Close - bars[i - 1].Close) * Math.Sign(bars[i - 1].Close - mean[i - 1])).ToArray();
                    return Outputs(("Req", increments.Select((_, i) => Window(increments, i, length).Sum()).ToArray()));
                });
            case IndicatorName.RegressionOscillator:
                return new("Rosc", new[] { "Rosc" }, bars =>
                {
                    var fit = RegressionEndpoints(Closes(bars), length);
                    return Outputs(("Rosc", bars.Select((b, i) => fit[i] == 0 ? 0 : 100 * (b.Close - fit[i]) / fit[i]).ToArray()));
                });
            case IndicatorName.RelativeSpreadStrength:
                return new("Rss", new[] { "Rss" }, bars =>
                {
                    var prices = Closes(bars).Select(BinaryDecimal).ToArray();
                    var fast = MotionDecimalAverage(prices, Integer(options, "FastLength", 10), kind);
                    var slow = MotionDecimalAverage(prices, Integer(options, "SlowLength", 40), kind);
                    var spread = fast.Zip(slow, (a, b) => a - b).ToArray();
                    var changes = spread.Select((v, i) => i == 0 ? 0 : v - spread[i - 1]).ToArray();
                    var gains = MotionDecimalAverage(changes.Select(v => Math.Max(0, v)).ToArray(), length, 6);
                    var losses = MotionDecimalAverage(changes.Select(v => Math.Max(0, -v)).ToArray(), length, 6);
                    var rsi = gains.Select((v, i) => losses[i] == 0 ? 100 : 100 * v / (v + losses[i])).ToArray();
                    for (var i = 1; i < rsi.Length; i++)
                        if (length > 1 && changes[i] == 0) rsi[i] = rsi[i - 1];
                    return Outputs(("Rss", MotionDecimalAverage(rsi, Integer(options, "SmoothLength", 5), kind).Select(v => (double)v).ToArray()));
                });
            case IndicatorName.RecursiveDifferenciator:
                return new("Rd", new[] { "Rd" }, bars =>
                {
                    var source = MotionRsi(Average(Closes(bars), length, kind), length).Select(v => v / 100).ToArray();
                    var alpha = Number(options, .6, "Alpha");
                    // Expand 1 / (1-(1-alpha)z^-1+(1-alpha)z^-(length+1)), then convolve.
                    var impulse = new double[bars.Count];
                    if (impulse.Length > 0) impulse[0] = 1;
                    for (var k = 1; k < impulse.Length; k++)
                        impulse[k] = (1 - alpha) * (impulse[k - 1] - (k <= length ? 0 : impulse[k - length - 1]));
                    return Outputs(("Rd", source.Select((_, i) => Enumerable.Range(0, i + 1)
                        .Sum(j => impulse[i - j] * source[j] * (j == 0 ? 1 : alpha))).ToArray()));
                });
            case IndicatorName.RatioOCHLAverager:
                return new("Rochla", new[] { "Rochla" }, bars =>
                {
                    var gains = bars.Select(b => b.High == b.Low ? 0 : Math.Min(1, Math.Abs(b.Close - b.Open) / (b.High - b.Low))).ToArray(); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    return Outputs(("Rochla", ExpandedGainTrajectory(Closes(bars), gains)));
                });
            case IndicatorName.RetentionAccelerationFilter:
                return new("Raf", new[] { "Raf" }, bars =>
                {
                    var gains = bars.Select((_, i) =>
                    {
                        var first = Window(bars, i, length).ToArray();
                        var second = Window(bars, i, length * 2).ToArray();
                        var high1 = first.Max(b => b.High);
                        var high2 = second.Max(b => b.High);
                        var a = 2 * (high1 - first.Min(b => b.Low));
                        var b = 2 * (high2 - second.Min(b => b.Low));
                        // Cancel the k1/k2 and alpha factors in r2/r1. Keep the original zero-r1 cases.
                        var ratio = high1 <= 0 || a == 0 || b == 0 || a == 1 || b == 1 || a == b // NOSONAR: S1244 - Preserve the original exact singular cases before algebraic cancellation.
                            ? 0 : Math.Sqrt(high2 / high1) * a / b;
                        return Math.Pow(Math.Min(1, ratio), Math.Sqrt(length)) / length;
                    }).ToArray();
                    return Outputs(("Raf", ExpandedGainTrajectory(Closes(bars), gains)));
                });
            case IndicatorName.TrigonometricOscillator:
                return new("To", new[] { "To" }, bars =>
                {
                    var fitted = RegressionEndpoints(Closes(bars), length);
                    // The integer-sign arcsines and banker's rounding reduce to three exact angles.
                    var angles = fitted.Select((v, i) => v > (i == 0 ? 0 : fitted[i - 1]) ? Math.PI :
                        v < (i == 0 ? 0 : fitted[i - 1]) ? -3 * Math.PI : 0).ToArray();
                    return Outputs(("To", RegressionEndpoints(angles, length).Select(Math.Atan).ToArray()));
                });
            case IndicatorName.KaseIndicator:
                return new("KaseUp", new[] { "KaseUp", "KaseDn" }, bars => KaseRatioOutputs(bars, indicator));
            case IndicatorName.KaseDevStopV2:
                return new("Dev1", new[] { "Dev1", "Dev2", "Dev3", "Dev4" }, bars => KaseStopV2Outputs(bars, indicator));
            case IndicatorName.KaseDevStopV1:
                return new("Dev1", new[] { "Dev1", "Dev2", "Dev3", "WarningLine" }, bars => KaseStopV1Outputs(bars, indicator));
            case IndicatorName.MultiLevelIndicator:
                return new("Mli", new[] { "Mli" }, bars => Outputs(("Mli", bars.Select((b, i) =>
                    ((i < length ? 0 : bars[i - length].Open) - b.Open) * Number(options, 10000, "Factor")).ToArray())));
            case IndicatorName.ReversalPoints:
                return new("Rp", new[] { "Rp" }, bars =>
                {
                    var smooth = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d)));
                    var movement = bars.Select((b, i) => Math.Abs(b.Close - (i == 0 ? 0 : bars[i - 1].Close))).ToArray();
                    var first = Average(movement, smooth, kind);
                    var second = Average(first, smooth, kind);
                    var ratio = first.Select((v, i) => second[i] == 0 ? 0 : v / second[i]).ToArray();
                    return Outputs(("Rp", ratio.Select((_, i) => Window(ratio, i, length).Sum()).ToArray()));
                });
            case IndicatorName.SimpleCycle:
                return new("Sc", new[] { "Sc" }, bars =>
                {
                    var a = 1d / length;
                    var beta = Math.Max(.01, Math.Min(.99, 2d / (length + 1)));
                    var q = 1 - beta;
                    // Eliminate the source-feedback and EMA states algebraically, then expand the
                    // resulting rational transfer function and convolve its impulse with prices.
                    var numerator = new double[length + 2];
                    numerator[0] += a; numerator[1] -= a * q;
                    numerator[length] -= a; numerator[length + 1] += a * q;
                    var denominator = new double[length + 3];
                    denominator[0] = 1; denominator[1] -= 2 * q + a * beta; denominator[2] += q;
                    denominator[length + 1] += a; denominator[length + 2] -= a * q;
                    var impulse = new double[bars.Count];
                    for (var i = 0; i < impulse.Length; i++)
                        impulse[i] = (i < numerator.Length ? numerator[i] : 0) -
                            Enumerable.Range(1, Math.Min(i, denominator.Length - 1)).Sum(j => denominator[j] * impulse[i - j]);
                    return Outputs(("Sc", bars.Select((_, i) => Enumerable.Range(0, i + 1)
                        .Sum(j => impulse[i - j] * bars[j].Close)).ToArray()));
                });
            case IndicatorName.SimpleLines:
                return new("Sl", new[] { "Sl" }, bars =>
                {
                    var result = new double[bars.Count];
                    var multiplier = BinaryDecimal(Number(options, 10, "Multiplier"));
                    var anchor = bars.Count == 0 ? 0 : BinaryDecimal(bars[0].Close);
                    long ticks = 0, previousTicks = 0;
                    for (var i = 0; i < result.Length; i++)
                    {
                        var price = (BinaryDecimal(bars[i].Close) - anchor) * length;
                        var displacement = price - ticks + multiplier * (ticks - (i == 1 ? price : previousTicks));
                        var next = i == 0 || Math.Abs(displacement) <= 1 ? ticks : ticks + Math.Sign(displacement);
                        previousTicks = ticks; ticks = next;
                        result[i] = (double)(anchor + (decimal)ticks / length);
                    }
                    return Outputs(("Sl", result));
                });
            default: return null;
        }
    }

    private static double[] KaseReferencePeak(IReadOnlyList<Bar> bars, int length, int smooth)
    {
        var atr = Average(TrueRanges(bars), length, 6);
        var difference = bars.Select((b, i) => atr[i] == 0 ? 0 : Math.Sqrt(length) / atr[i] *
            (b.High + b.Low - (i < length ? 0 : bars[i - length].High + bars[i - length].Low))).ToArray();
        return Average(difference, smooth, 2);
    }

    private static decimal BinaryDecimal(double value)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 2047);
        var significand = bits & ((1L << 52) - 1);
        if (exponent != 0) significand |= 1L << 52;
        decimal result = significand;
        var shift = exponent == 0 ? -1074 : exponent - 1075;
        while (shift < 0) { result /= 2; shift++; }
        while (shift > 0) { result *= 2; shift--; }
        return bits < 0 ? -result : result;
    }

    private static decimal[] MotionDecimalAverage(decimal[] values, int period, int kind)
    {
        if (kind is 4 or 5)
        {
            var first = MotionDecimalAverage(values, period, 3);
            var second = MotionDecimalAverage(first, period, 3);
            var third = kind == 5 ? MotionDecimalAverage(second, period, 3) : second;
            return first.Select((v, i) => kind == 4 ? 2 * v - second[i] : 3 * (v - second[i]) + third[i]).ToArray();
        }
        var gain = kind == 6 ? 1m / period : 2m / (period + 1);
        var powers = new decimal[values.Length + 1]; powers[0] = 1;
        for (var i = 1; i < powers.Length; i++) powers[i] = powers[i - 1] * (1 - gain);
        return values.Select((_, i) =>
        {
            if (kind == 1) return i + 1 < period ? 0 : Window(values, i, period).Sum() / period;
            if (kind == 2) return Enumerable.Range(Math.Max(0, i - period + 1), Math.Min(period, i + 1))
                .Sum(j => values[j] * (period - i + j)) / (period * (period + 1m) / 2);
            if (kind == 3 && i < period) return values.Take(i + 1).Average();
            var start = kind == 3 ? period : 0;
            // Center the convolution before applying weights: a constant input must remain
            // exactly constant, even when rounded decimal powers do not sum to exactly one.
            var offset = kind == 3 ? values[0] : 0;
            var seed = kind == 3 ? values.Take(period).Average(v => v - offset) * powers[i - period + 1] : 0;
            return offset + seed + Enumerable.Range(start, i - start + 1).Sum(j => (values[j] - offset) * gain * powers[i - j]);
        }).ToArray();
    }

    private static double[] MotionRsi(double[] values, int length, int kind = 6)
    {
        var changes = values.Select((v, i) => i == 0 ? 0 : v - values[i - 1]).ToArray();
        var gains = Average(changes.Select(v => Math.Max(0, v)).ToArray(), length, kind);
        var losses = Average(changes.Select(v => Math.Max(0, -v)).ToArray(), length, kind);
        var result = gains.Select((v, i) => losses[i] == 0 ? 100 : 100 * v / (v + losses[i])).ToArray();
        for (var i = 1; i < result.Length; i++)
            if (length > 1 && kind is 3 or 6 && values[i] == values[i - 1]) result[i] = result[i - 1]; // NOSONAR: S1244 - Only identical observations select the unchanged-price recurrence.
        return result;
    }
}
