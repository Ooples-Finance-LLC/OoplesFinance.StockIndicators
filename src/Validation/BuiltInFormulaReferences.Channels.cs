using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? Channels(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 20);
        var kind = AverageKind(options, name == IndicatorName.MovingAverageEnvelope ? 1 : 3);
        if (kind == 0) return null;
        switch (name)
        {
            case IndicatorName.PriceLineChannel:
            case IndicatorName.PriceCurveChannel:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var curved = name == IndicatorName.PriceCurveChannel;
                    var atr = Average(TrueRanges(bars), length, kind);
                    var upper = new double[bars.Count];
                    var lower = new double[bars.Count];
                    var upperCandidates = new double[bars.Count];
                    var lowerCandidates = new double[bars.Count];
                    double upperSize = 0, lowerSize = 0, sharedSize = 0;
                    var lastUpperRise = -1;
                    var lastLowerFall = -1;
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var rise = (i == 0 ? bars[i].Close : upper[i - 1]) > (i < 2 ? 0 : upper[i - 2]);
                        var fall = (i == 0 ? bars[i].Close : lower[i - 1]) < (i < 2 ? 0 : lower[i - 2]);
                        if (i == 0) upperSize = lowerSize = sharedSize = atr[i] / length;
                        if (rise) { upperSize = atr[i]; lastUpperRise = i; }
                        if (fall) { lowerSize = atr[i]; lastLowerFall = i; }
                        if (rise || fall) sharedSize = atr[i];
                        var upStep = curved ? sharedSize * (i - lastUpperRise + 1) / ((double)length * length) : upperSize / length;
                        var downStep = curved ? sharedSize * (i - lastLowerFall + 1) / ((double)length * length) : lowerSize / length;
                        // Explicit envelopes of all historical prices after the intervening drift steps.
                        // Include the current price both before and after drift, as the contract clamps to price.
                        for (var j = 0; j < i; j++) { upperCandidates[j] -= upStep; lowerCandidates[j] += downStep; }
                        upperCandidates[i] = Math.Max(bars[i].Close, bars[i].Close - upStep);
                        lowerCandidates[i] = Math.Min(bars[i].Close, bars[i].Close + downStep);
                        upper[i] = upperCandidates.Take(i + 1).Max();
                        lower[i] = lowerCandidates.Take(i + 1).Min();
                    }
                    return Outputs(("UpperBand", upper), ("MiddleBand", upper.Zip(lower, (a, b) => (a + b) / 2).ToArray()),
                        ("LowerBand", lower));
                });
            case IndicatorName.RateOfChangeBands:
                return new("Roc", new[] { "UpperBand", "MiddleBand", "LowerBand", "Roc" }, bars =>
                {
                    var roc = bars.Select((b, i) => i < length || bars[i - length].Close == 0 ? 0 :
                        100 * (b.Close - bars[i - length].Close) / bars[i - length].Close).ToArray();
                    var upper = roc.Select((_, i) => Math.Sqrt(Window(roc, i, length).Average(v => v * v))).ToArray();
                    return Outputs(("UpperBand", upper), ("MiddleBand", new double[bars.Count]),
                        ("LowerBand", upper.Select(v => -v).ToArray()), ("Roc", Average(roc, Integer(options, "SmoothLength", 3), kind)));
                });
            case IndicatorName.ScalpersChannel:
                return new("Scalper", new[] { "UpperBand", "MiddleBand", "LowerBand", "Scalper" }, bars =>
                {
                    var period = Integer(options, "Length1", 15);
                    var smooth = Integer(options, "Length2", 20);
                    var upper = bars.Select((_, i) => Window(bars, i, period).Max(b => b.High)).ToArray();
                    var lower = bars.Select((_, i) => Window(bars, i, period).Min(b => b.Low)).ToArray();
                    var atr = Average(TrueRanges(bars), smooth, kind);
                    var mean = Average(Closes(bars), smooth, kind);
                    return Outputs(("UpperBand", upper), ("MiddleBand", upper.Zip(lower, (a, b) => (a + b) / 2).ToArray()),
                        ("LowerBand", lower), ("Scalper", mean.Select((v, i) => atr[i] > 0 ? v - Math.Log(Math.PI * atr[i]) : v).ToArray()));
                });
            case IndicatorName.StationaryExtrapolatedLevels:
                return new("Deviation", new[] { "UpperBand", "MiddleBand", "LowerBand", "Deviation" }, bars =>
                {
                    var mean = Average(Closes(bars), length, kind);
                    var residuals = bars.Select((b, i) => b.Close - mean[i]).ToArray();
                    var extrapolated = bars.Select((_, i) =>
                    {
                        if (i <= length) return 0d;
                        var recent = residuals[i - length];
                        var older = i < 2 * length ? 0 : residuals[i - 2 * length];
                        if (recent == older) return 0; // NOSONAR: S1244 - Exact endpoint ties select the defined zero-output branch.
                        return i < 2 * length ? recent * i / (2d * (i - length)) : recent - older / 2;
                    }).ToArray();
                    // Two trailing extrema filters compose into one window of summed lengths minus one.
                    var width = length + Math.Max(2, length) - 1;
                    var upper = extrapolated.Select((_, i) => Window(extrapolated, i, width).Max()).ToArray();
                    var lower = extrapolated.Select((_, i) => Window(extrapolated, i, width).Min()).ToArray();
                    return Outputs(("UpperBand", upper), ("MiddleBand", upper.Zip(lower, (a, b) => (a + b) / 2).ToArray()),
                        ("LowerBand", lower), ("Deviation", residuals));
                });
            case IndicatorName.TironeLevels:
            case IndicatorName.ProjectedSupportAndResistance:
                var tirone = name == IndicatorName.TironeLevels;
                var projectionKeys = tirone ? new[] { "Tlh", "Clh", "Blh", "Am", "Eh", "El", "Rh", "Rl" }
                    : new[] { "Support1", "Support2", "Resistance1", "Resistance2", "MiddleBand" };
                return new(tirone ? "Am" : "MiddleBand", projectionKeys, bars =>
                {
                    var levels = projectionKeys.ToDictionary(k => k, _ => new double[bars.Count]);
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var window = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).ToArray();
                        var high = window.Max(b => b.High);
                        var low = window.Min(b => b.Low);
                        var center = (high + low) / 2;
                        var halfRange = (high - low) / 2;
                        var mean = (2 * center + bars[i].Close) / 3;
                        var values = tirone
                            ? new[] { center + halfRange / 3, center, center - halfRange / 3, mean,
                                mean + 2 * halfRange, mean - 2 * halfRange, 2 * mean - center + halfRange, 2 * mean - center - halfRange }
                            : new[] { center - 1.5 * halfRange, center - 2 * halfRange, center + 1.5 * halfRange, center + 2 * halfRange, center };
                        for (var j = 0; j < values.Length; j++) levels[projectionKeys[j]][i] = values[j];
                    }
                    return levels;
                });
            case IndicatorName.FractalChaosBands:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var upper = new double[bars.Count];
                    var lower = new double[bars.Count];
                    for (var i = 4; i < bars.Count; i++)
                    {
                        var window = Window(bars, i, 5).ToArray();
                        var center = window[2];
                        var highRank = window.Count(b => b.High < center.High);
                        var lowRank = window.Count(b => b.Low > center.Low);
                        upper[i] = highRank == 4 ? center.High : upper[i - 1];
                        lower[i] = lowRank == 4 ? center.Low : lower[i - 1];
                    }
                    return Outputs(("UpperBand", upper), ("LowerBand", lower),
                        ("MiddleBand", upper.Zip(lower, (u, l) => (u + l) / 2).ToArray()));
                });
            case IndicatorName.ExtendedRecursiveBands:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var upper = new double[bars.Count];
                    var lower = new double[bars.Count];
                    var gain = 2d / (length + 1);
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var price = bars[i].Close;
                        var priorUpper = i == 0 ? price : upper[i - 1];
                        var priorLower = i == 0 ? price : lower[i - 1];
                        // Convex blends on each side; gain <= 1/2 keeps the upper blend above the lower.
                        upper[i] = gain * Math.Min(price, priorUpper) + (1 - gain) * Math.Max(price, priorUpper);
                        lower[i] = gain * Math.Max(price, priorLower) + (1 - gain) * Math.Min(price, priorLower);
                    }
                    return Outputs(("UpperBand", upper), ("LowerBand", lower),
                        ("MiddleBand", upper.Zip(lower, (u, l) => (u + l) / 2).ToArray()));
                });
            case IndicatorName.FlaggingBands:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand", "TrailingStop" }, bars =>
                {
                    var prices = Closes(bars);
                    var variance = PopulationVariance(prices, length);
                    var upper = new double[bars.Count];
                    var lower = new double[bars.Count];
                    var middle = new double[bars.Count];
                    var stop = new double[bars.Count];
                    var uptrend = false;
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var price = prices[i];
                        var previousUpper = i == 0 ? price : upper[i - 1];
                        var previousLower = i == 0 ? price : lower[i - 1];
                        var olderUpper = i < 2 ? price : upper[i - 2];
                        var olderLower = i < 2 ? price : lower[i - 2];
                        var decay = Math.Sqrt(variance[i]) / length;
                        upper[i] = Math.Max(price, previousUpper - (previousUpper == olderUpper ? decay : 0)); // NOSONAR: S1244 - Decay applies only to an unchanged upper bound.
                        lower[i] = Math.Min(price, previousLower + (previousLower == olderLower ? decay : 0)); // NOSONAR: S1244 - Decay applies only to an unchanged lower bound.
                        if (price > olderUpper) uptrend = true;
                        else if (price < olderLower) uptrend = false;
                        var upperWeight = uptrend ? .75 : .25;
                        middle[i] = upperWeight * upper[i] + (1 - upperWeight) * lower[i];
                        stop[i] = uptrend ? lower[i] : upper[i];
                    }
                    return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower), ("TrailingStop", stop));
                });
            case IndicatorName.PercentageTrailingStops:
                var fraction = Number(options, 10, "Pct") / 100;
                return new("LongStop", new[] { "LongStop", "ShortStop" }, bars =>
                {
                    var longs = new double[bars.Count];
                    var shorts = new double[bars.Count];
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var high = i == 0 ? bars[i].Close : Window(bars, i - 1, length).Max(b => b.High);
                        var low = i == 0 ? bars[i].Close : Window(bars, i - 1, length).Min(b => b.Low);
                        longs[i] = bars[i].High > high ? (1 - fraction) * bars[i].High : i == 0 ? bars[i].Close : longs[i - 1];
                        shorts[i] = bars[i].Low < low ? (1 + fraction) * bars[i].Low : i == 0 ? bars[i].Close : shorts[i - 1];
                    }
                    return Outputs(("LongStop", longs), ("ShortStop", shorts));
                });
            case IndicatorName.EfficientTrendStepChannel:
                var fastPeriod = Integer(options, "FastLength", 50);
                var slowPeriod = Integer(options, "SlowLength", 200);
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var efficiency = EfficiencyRatios(bars, length);
                    var fastVariance = PopulationVariance(Closes(bars), fastPeriod);
                    var slowVariance = PopulationVariance(Closes(bars), slowPeriod);
                    var width = efficiency.Select((e, i) => 2 * (e * Math.Sqrt(fastVariance[i]) + (1 - e) * Math.Sqrt(slowVariance[i]))).ToArray();
                    var middle = new double[bars.Count];
                    for (var i = 0; i < middle.Length; i++)
                    {
                        var prior = i == 0 ? bars[i].Close : middle[i - 1];
                        middle[i] = Math.Abs(bars[i].Close - prior) > width[i] ? bars[i].Close : prior;
                    }
                    return Outputs(("MiddleBand", middle), ("UpperBand", middle.Select((m, i) => m + width[i]).ToArray()),
                        ("LowerBand", middle.Select((m, i) => m - width[i]).ToArray()));
                });
            case IndicatorName.KaufmanAdaptiveBands:
                var exponent = Number(options, 3, "StdDevFactor");
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var gains = EfficiencyRatios(bars, length).Select(e => Math.Pow(e, exponent)).ToArray();
                    var middle = new double[bars.Count];
                    var deviation = new double[bars.Count];
                    for (var i = 0; i < middle.Length; i++)
                    {
                        // Expand the variable-gain observation weights, including the initial zero observation.
                        var weights = new double[i + 1];
                        var retained = 1d;
                        for (var j = i; j >= 0; j--) { weights[j] = gains[j] * retained; retained *= 1 - gains[j]; }
                        middle[i] = weights.Select((w, j) => w * bars[j].Close).Sum();
                        var variance = retained * middle[i] * middle[i] + weights.Select((w, j) => w * Math.Pow(bars[j].Close - middle[i], 2)).Sum();
                        deviation[i] = Math.Sqrt(Math.Max(0, variance));
                    }
                    return Outputs(("MiddleBand", middle), ("UpperBand", middle.Select((m, i) => m + deviation[i]).ToArray()),
                        ("LowerBand", middle.Select((m, i) => m - deviation[i]).ToArray()));
                });
            case IndicatorName.DEnvelope:
                var deviationFactor = Number(options, 2, "DevFactor");
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var alpha = 2d / (length + 1);
                    var pole = 1 - alpha;
                    // Cancel the apparent (1-alpha) singularity in the transfer function first.
                    var impulse = Enumerable.Range(0, bars.Count).Select(j => j == 0 ? 2 * alpha
                        : alpha * (2 * (j + 1) * Math.Pow(pole, j) - (2 - alpha) * j * Math.Pow(pole, j - 1))).ToArray();
                    double[] Filter(double[] input) => input.Select((_, i) => Enumerable.Range(0, i + 1)
                        .Sum(j => impulse[j] * input[i - j])).ToArray();
                    var middle = Filter(Closes(bars));
                    var deviations = Filter(bars.Select((b, i) => Math.Abs(b.Close - middle[i])).ToArray());
                    var width = deviations.Select(d => deviationFactor * Math.Max(0, d)).ToArray();
                    return Outputs(("MiddleBand", middle), ("UpperBand", middle.Select((m, i) => m + width[i]).ToArray()),
                        ("LowerBand", middle.Select((m, i) => m - width[i]).ToArray()));
                });
            case IndicatorName.NickRypockTrailingReverse:
                return new("Nrtr", new[] { "Nrtr" }, bars =>
                {
                    var fraction = length / 100d;
                    var line = new double[bars.Count];
                    var rising = true;
                    var extreme = 0d;
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var price = bars[i].Close;
                        extreme = rising ? Math.Max(extreme, price) : Math.Min(extreme, price);
                        var threshold = extreme * (rising ? 1 - fraction : 1 + fraction);
                        var reverse = rising ? price <= threshold : price > threshold;
                        if (reverse) { rising = !rising; extreme = price; }
                        line[i] = extreme * (rising ? 1 - fraction : 1 + fraction);
                    }
                    return Outputs(("Nrtr", line));
                });
            case IndicatorName.GChannels:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var upper = new double[bars.Count];
                    var lower = new double[bars.Count];
                    double center = 0, halfWidth = 0;
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var rise = Math.Max(0, bars[i].Close - (center + halfWidth));
                        var fall = Math.Min(0, bars[i].Close - (center - halfWidth));
                        center += (rise + fall) / 2;
                        halfWidth = halfWidth * (1 - 2d / length) + (rise - fall) / 2;
                        upper[i] = center + halfWidth;
                        lower[i] = center - halfWidth;
                    }
                    return Outputs(("UpperBand", upper), ("LowerBand", lower),
                        ("MiddleBand", upper.Zip(lower, (u, l) => (u + l) / 2).ToArray()));
                });
            case IndicatorName.SmartEnvelope:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var upper = new double[bars.Count];
                    var lower = new double[bars.Count];
                    var response = Number(options, 1, "Factor") / length;
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var price = bars[i].Close;
                        if (i == 0) { upper[i] = lower[i] = price; continue; }
                        var movement = Math.Abs(price - bars[i - 1].Close);
                        var upperGain = i > 1 && lower[i - 1] < lower[i - 2] ? -response : response;
                        var lowerGain = i > 1 && upper[i - 1] > upper[i - 2] ? -response : response;
                        upper[i] = Math.Max(price, upper[i - 1]) - upperGain * Math.Min(Math.Abs(price - upper[i - 1]), movement);
                        lower[i] = Math.Min(price, lower[i - 1]) + lowerGain * Math.Min(Math.Abs(price - lower[i - 1]), movement);
                    }
                    return Outputs(("UpperBand", upper), ("LowerBand", lower),
                        ("MiddleBand", upper.Zip(lower, (u, l) => (u + l) / 2).ToArray()));
                });
            case IndicatorName.MeanAbsoluteDeviationBands:
            case IndicatorName.MeanAbsoluteErrorBands:
            case IndicatorName.RootMovingAverageSquaredErrorBands:
            case IndicatorName.TimeSeriesForecast:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var prices = Closes(bars);
                    var middle = name == IndicatorName.TimeSeriesForecast ? RegressionEndpoints(prices, length) : Average(prices, length, kind);
                    var errors = prices.Zip(middle, (p, m) => Math.Abs(p - m)).ToArray();
                    double[] width;
                    if (name == IndicatorName.MeanAbsoluteDeviationBands)
                        width = prices.Select((_, i) =>
                        {
                            var window = Window(prices, i, length).ToArray();
                            var mean = window.Average();
                            return window.Average(v => Math.Abs(v - mean));
                        }).ToArray();
                    else if (name == IndicatorName.RootMovingAverageSquaredErrorBands)
                        width = Average(errors.Select(v => v * v).ToArray(), length, kind).Select(Math.Sqrt).ToArray();
                    else
                        width = errors.Select((_, i) => errors.Take(i + 1).Average()).ToArray();
                    var multiplier = Number(options, 1, "StdDevFactor");
                    return Outputs(("MiddleBand", middle), ("UpperBand", middle.Zip(width, (v, w) => v + multiplier * w).ToArray()),
                        ("LowerBand", middle.Zip(width, (v, w) => v - multiplier * w).ToArray()));
                });
            case IndicatorName.InterquartileRangeBands:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var upper = new double[bars.Count];
                    var lower = new double[bars.Count];
                    var multiplier = Number(options, 1.5, "Mult");
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var ordered = Window(bars, i, length).Select(b => b.Close).OrderBy(v => v).ToArray();
                        var q1 = ordered[(ordered.Length + 3) / 4 - 1];
                        var q3 = ordered[(3 * ordered.Length + 3) / 4 - 1];
                        upper[i] = q3 + multiplier * (q3 - q1);
                        lower[i] = q1 - multiplier * (q3 - q1);
                    }
                    return Outputs(("UpperBand", upper), ("LowerBand", lower),
                        ("MiddleBand", upper.Zip(lower, (u, l) => (u + l) / 2).ToArray()));
                });
            case IndicatorName.RangeBands:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var middle = Average(Closes(bars), length, kind);
                    var width = middle.Select((_, i) => Number(options, 1, "StdDevFactor") *
                        (Window(middle, i, length).Max() - Window(middle, i, length).Min())).ToArray();
                    return Outputs(("MiddleBand", middle), ("UpperBand", middle.Zip(width, (v, w) => v + w).ToArray()),
                        ("LowerBand", middle.Zip(width, (v, w) => v - w).ToArray()));
                });
            case IndicatorName.RangeIdentifier:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var upper = new double[bars.Count];
                    var lower = new double[bars.Count];
                    // Retain the last breakout candle's range while closes remain strictly inside it.
                    var anchor = 0;
                    for (var i = 0; i < bars.Count; i++)
                    {
                        if (bars[i].Close <= bars[anchor].Low || bars[i].Close >= bars[anchor].High) anchor = i;
                        upper[i] = bars[anchor].High;
                        lower[i] = bars[anchor].Low;
                    }
                    return Outputs(("UpperBand", upper), ("LowerBand", lower),
                        ("MiddleBand", upper.Zip(lower, (u, l) => ExactPriceMean(u, l)).ToArray()));
                });
            case IndicatorName.MovingAverageDisplacedEnvelope:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var smoothed = Average(Closes(bars), Integer(options, "Length1", 9), kind);
                    var delay = Integer(options, "Length2", 13);
                    var pct = Number(options, .5, "Pct") / 100;
                    var middle = bars.Select((_, i) => i < delay ? 0 : smoothed[i - delay]).ToArray();
                    return Outputs(("MiddleBand", middle), ("UpperBand", middle.Select(v => v * (1 + pct)).ToArray()),
                        ("LowerBand", middle.Select(v => v * (1 - pct)).ToArray()));
                });
            case IndicatorName.ProjectionBands:
            case IndicatorName.ProjectionOscillator:
            case IndicatorName.ProjectionBandwidth:
                var projectionKey = name == IndicatorName.ProjectionBands ? "MiddleBand"
                    : name == IndicatorName.ProjectionOscillator ? "Pbo" : "Pbw";
                return new(projectionKey, name == IndicatorName.ProjectionBands ? new[] { "UpperBand", "MiddleBand", "LowerBand" }
                    : new[] { projectionKey, "Signal" }, bars =>
                {
                    var (upper, lower) = ReferenceProjectionEnvelope(bars, length);
                    if (name == IndicatorName.ProjectionBands)
                        return Outputs(("UpperBand", upper), ("LowerBand", lower),
                            ("MiddleBand", upper.Zip(lower, (u, l) => (u + l) / 2).ToArray()));
                    var line = bars.Select((b, i) => name == IndicatorName.ProjectionOscillator
                        ? upper[i] == lower[i] ? 0 : 100 * (b.Close - lower[i]) / (upper[i] - lower[i]) // NOSONAR: S1244 - Equal bounds define an exactly zero range; nearby distinct bounds must still be evaluated.
                        : upper[i] + lower[i] == 0 ? 0 : 200 * (upper[i] - lower[i]) / (upper[i] + lower[i])).ToArray();
                    return Outputs((projectionKey, line), ("Signal", Average(line,
                        name == IndicatorName.ProjectionOscillator ? Integer(options, "SmoothLength", 4) : length, kind)));
                });
            case IndicatorName.MovingAverageBands:
            case IndicatorName.MovingAverageBandWidth:
                var bandwidth = name == IndicatorName.MovingAverageBandWidth;
                return new(bandwidth ? "Mabw" : "MiddleBand", bandwidth ? new[] { "Mabw" }
                    : new[] { "UpperBand", "MiddleBand", "LowerBand", "FastMa" }, bars =>
                {
                    var period = Integer(options, "FastLength", 10);
                    var prices = Closes(bars);
                    var fast = Average(prices, period, kind);
                    var slow = Average(prices, Integer(options, "SlowLength", 50), kind);
                    var energy = fast.Zip(slow, (f, s) => (f - s) * (f - s)).ToArray();
                    var multiplier = Number(options, 1, "Mult");
                    var width = energy.Select((_, i) => multiplier * Math.Sqrt(Window(energy, i, period).Average())).ToArray();
                    if (bandwidth) return Outputs(("Mabw", slow.Select((s, i) => s == 0 ? 0 : 200 * width[i] / s).ToArray()));
                    return Outputs(("MiddleBand", slow), ("FastMa", fast),
                        ("UpperBand", slow.Select((s, i) => s + width[i]).ToArray()),
                        ("LowerBand", slow.Select((s, i) => s - width[i]).ToArray()));
                });
            case IndicatorName.NarrowSidewaysChannel:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var prices = Closes(bars);
                    var middle = Average(prices, length, kind);
                    var width = PopulationVariance(prices, length).Select(v => 3 * Math.Sqrt(v)).ToArray();
                    return Outputs(("MiddleBand", middle), ("UpperBand", middle.Select((v, i) => v + width[i]).ToArray()),
                        ("LowerBand", middle.Select((v, i) => v - width[i]).ToArray()));
                });
            case IndicatorName.UniChannel:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var center = Average(Closes(bars), length, kind);
                    var upper = Number(options, .02, "UbFac");
                    var lower = Number(options, .02, "LbFac");
                    var additive = options.GetType().GetProperty("Type1")?.GetValue(options) is true;
                    return Outputs(("MiddleBand", center), ("UpperBand", center.Select(v => additive ? v + upper : v * (1 + upper)).ToArray()),
                        ("LowerBand", center.Select(v => additive ? v - lower : v * (1 - lower)).ToArray()));
                });
            case IndicatorName.MovingAverageSupportResistance:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var center = Average(Closes(bars), length, kind);
                    var factor = 1 + Number(options, 2, "Factor") / 100;
                    return Outputs(("MiddleBand", center), ("UpperBand", center.Select(v => v * factor).ToArray()),
                        ("LowerBand", center.Select(v => factor == 0 ? 0 : v / factor).ToArray()));
                });
            case IndicatorName.PriceHeadleyAccelerationBands:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var factor = 2000 * Number(options, .001, "Factor");
                    var shifts = bars.Select(b => (b.High + b.Low) == 0 ? 0 : factor * (b.High - b.Low) / ((b.High + b.Low) / 2)).ToArray();
                    var upper = bars.Select((b, i) => b.High * (1 + shifts[i])).ToArray();
                    var lower = bars.Select((b, i) => b.Low * (1 - shifts[i])).ToArray();
                    return Outputs(("MiddleBand", Average(Closes(bars), length, kind)),
                        ("UpperBand", Average(upper, length, kind)), ("LowerBand", Average(lower, length, kind)));
                });
            case IndicatorName.SmoothedVolatilityBands:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var fast = Integer(options, "Length1", 20);
                    var prices = Closes(bars);
                    var center = Average(prices, Integer(options, "Length2", 21), kind);
                    var baseline = Average(prices, fast, kind);
                    var atr = Average(TrueRanges(bars), 2 * fast - 1, kind);
                    var deviation = Number(options, 2.4, "Deviation");
                    var adjustment = Number(options, .9, "BandAdjust");
                    var width = prices.Select((v, i) => v == 0 ? 0 : deviation * atr[i] * baseline[i] / v).ToArray();
                    return Outputs(("MiddleBand", center), ("UpperBand", baseline.Select((v, i) => v + width[i]).ToArray()),
                        ("LowerBand", baseline.Select((v, i) => v - adjustment * width[i]).ToArray()));
                });
            case IndicatorName.TrendTraderBands:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var prices = Closes(bars);
                    var atr = Average(TrueRanges(bars), length, kind);
                    var factor = Number(options, 3, "Mult");
                    var step = Number(options, 20, "BandStep");
                    var levels = new double[bars.Count];
                    double retained = 0;
                    for (var i = 1; i < bars.Count; i++)
                    {
                        var window = Window(prices, i - 1, Math.Max(2, length)).ToArray();
                        var upper = window.Max() - factor * atr[i - 1];
                        var lower = window.Min() + factor * atr[i - 1];
                        if (prices[i] > Math.Max(upper, lower)) retained = upper;
                        else if (prices[i] < Math.Min(upper, lower)) retained = lower;
                        levels[i] = retained;
                    }
                    var center = Average(levels, length, kind);
                    return Outputs(("MiddleBand", center), ("UpperBand", center.Select(v => v + step).ToArray()),
                        ("LowerBand", center.Select(v => v - step).ToArray()));
                });
            case IndicatorName.SupportResistance:
                return new("Support", new[] { "Support", "Resistance" }, bars =>
                {
                    var prices = Closes(bars);
                    var mean = Average(prices, length, kind);
                    double[] Levels(bool support)
                    {
                        var resets = Enumerable.Range(0, bars.Count).Where(i => i == 0 || (support
                            ? prices[i - 1] < mean[i - 1] && prices[i] >= mean[i - 1]
                            : prices[i - 1] > mean[i - 1] && prices[i] <= mean[i - 1])).ToArray();
                        return prices.Select((_, i) =>
                        {
                            var reset = resets.Last(j => j <= i);
                            var window = Window(bars, reset, Math.Max(2, length));
                            return support ? window.Min(b => b.Low) : window.Max(b => b.High);
                        }).ToArray();
                    }
                    return Outputs(("Support", Levels(true)), ("Resistance", Levels(false)));
                });
            case IndicatorName.BollingerBandsFibonacciRatios:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var center = Average(Closes(bars), length, kind);
                    var atr = Average(TrueRanges(bars), length, kind);
                    var ratio = Number(options, 4.236, "FibRatio3");
                    return Outputs(("MiddleBand", center),
                        ("UpperBand", center.Select((v, i) => v + ratio * atr[i]).ToArray()),
                        ("LowerBand", center.Select((v, i) => v - ratio * atr[i]).ToArray()));
                });
            case IndicatorName.BollingerBandsWithAtrPct:
                var atrPercentCenterLength = Integer(options, "BbLength", 20);
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var center = Average(Closes(bars), atrPercentCenterLength, kind);
                    var fractions = bars.Select((b, i) =>
                    {
                        var previous = i == 0 ? 0 : bars[i - 1].Close;
                        var highGap = Math.Abs(b.High - previous);
                        var lowGap = Math.Abs(b.Low - previous);
                        var range = b.High - b.Low;
                        var largest = Math.Max(range, Math.Max(highGap, lowGap));
                        var denominator = largest == highGap ? previous + highGap / 2 // NOSONAR: S1244 - Select the operand returned by Max, preserving tie order.
                            : largest == lowGap ? b.Low + lowGap / 2 : (b.High + b.Low) / 2; // NOSONAR: S1244 - Select the operand returned by Max, preserving tie order.
                        return denominator == 0 ? 0 : largest / denominator;
                    }).ToArray();
                    var gain = 2d / (length + 1);
                    var multiplier = Number(options, 2, "StdDevMult");
                    var widths = fractions.Select((_, i) => center[i] * multiplier * Enumerable.Range(0, i + 1)
                        .Sum(j => gain * Math.Pow(1 - gain, i - j) * fractions[j])).ToArray();
                    return Outputs(("MiddleBand", center), ("UpperBand", center.Select((v, i) => v + widths[i]).ToArray()),
                        ("LowerBand", center.Select((v, i) => v - widths[i]).ToArray()));
                });
            case IndicatorName.DynamicSupportAndResistance:
                return new("MiddleBand", new[] { "Support", "Resistance", "MiddleBand" }, bars =>
                {
                    var atr = Average(TrueRanges(bars), length, AverageKind(options, 6));
                    var highs = bars.Select((_, i) => Window(bars, i, length).Max(b => b.High)).ToArray();
                    var lows = bars.Select((_, i) => Window(bars, i, length).Min(b => b.Low)).ToArray();
                    return Outputs(("Support", highs.Select((v, i) => v - Math.Sqrt(length) * atr[i]).ToArray()),
                        ("Resistance", lows.Select((v, i) => v + Math.Sqrt(length) * atr[i]).ToArray()),
                        ("MiddleBand", highs.Zip(lows, (h, l) => (h + l) / 2).ToArray()));
                });
            case IndicatorName.Dema2Lines:
                var demaFast = Integer(options, "FastLength", 10);
                var demaSlow = Integer(options, "SlowLength", 40);
                // This indicator publishes two successive smoothings, rather than DEMA lag cancellation.
                return new("Dema1", new[] { "Dema1", "Dema2" }, bars => Outputs(
                    ("Dema1", Average(Average(Closes(bars), demaFast, kind), demaFast, kind)),
                    ("Dema2", Average(Average(Closes(bars), demaSlow, kind), demaSlow, kind))));
            case IndicatorName.FibonacciRetrace:
                var retraceLength = Integer(options, "Length2", 50);
                var retraceFactor = Number(options, .382, "Factor");
                return new("UpperBand", new[] { "UpperBand", "LowerBand" }, bars =>
                {
                    var highs = bars.Select((_, i) => Window(bars, i, retraceLength).Max(b => b.High)).ToArray();
                    var lows = bars.Select((_, i) => Window(bars, i, retraceLength).Min(b => b.Low)).ToArray();
                    return Outputs(("UpperBand", highs.Select((v, i) => (1 - retraceFactor) * v + retraceFactor * lows[i]).ToArray()),
                        ("LowerBand", lows.Select((v, i) => (1 - retraceFactor) * v + retraceFactor * highs[i]).ToArray()));
                });
            case IndicatorName.DailyAveragePriceDelta:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("UpperBand", new[] { "UpperBand", "LowerBand" }, bars =>
                {
                    // Linearity: mean(high)-mean(low) = mean(high-low).
                    var extension = Average(bars.Select(b => b.High - b.Low).ToArray(), length, kind);
                    return Outputs(("UpperBand", bars.Select((b, i) => b.High + extension[i]).ToArray()),
                        ("LowerBand", bars.Select((b, i) => b.Low - extension[i]).ToArray()));
                });
            case IndicatorName.ContractHighLow:
                return new("Ch", new[] { "Ch", "Cl" }, bars => Outputs(
                    ("Ch", bars.Select((_, i) => bars.Take(i + 1).Max(b => b.High)).ToArray()),
                    ("Cl", bars.Select((_, i) => bars.Take(i + 1).Min(b => b.Low)).ToArray())));
            case IndicatorName.BollingerBandsAverageTrueRange:
                kind = AverageKind(options, 1);
                var bandAtrLength = Integer(options, "AtrLength", 22);
                var atrBandMultiplier = Number(options, 2, "StdDevMult", "Multiplier");
                return new("AtrDev", new[] { "AtrDev" }, bars =>
                {
                    var atr = Average(TrueRanges(bars), bandAtrLength, kind);
                    var variance = PopulationVariance(Closes(bars), length);
                    return Outputs(("AtrDev", atr.Select((v, i) =>
                    {
                        var width = 2 * atrBandMultiplier * Math.Sqrt(variance[i]);
                        return width == 0 ? 0 : v / width;
                    }).ToArray()));
                });
            case IndicatorName.AverageTrueRangeTrailingStops:
                var atrStopFactor = Number(options, 3, "Multiplier", "Factor");
                return new("Atrts", new[] { "Atrts" }, bars =>
                {
                    var trend = Average(Closes(bars), 63, kind);
                    var distance = Average(TrueRanges(bars), length, kind);
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var previous = i == 0 ? bars[i].Close : result[i - 1];
                        // Project the previous stop onto the trend's permitted half-line.
                        var direction = bars[i].Close > trend[i] ? 1 : -1;
                        var boundary = bars[i].Close - direction * atrStopFactor * distance[i];
                        result[i] = previous + direction * Math.Max(0, direction * (boundary - previous));
                    }
                    return Outputs(("Atrts", result));
                });
            case IndicatorName.AdaptiveTrailingStop:
                var stopPower = Number(options, 3, "Multiplier", "Factor");
                return new("Ts", new[] { "Ts" }, bars =>
                {
                    var efficiency = EfficiencyRatios(bars, length);
                    var result = new double[bars.Count];
                    var upperEnvelope = bars.Count == 0 ? 0 : bars[0].Close;
                    var lowerEnvelope = upperEnvelope;
                    double upperStop = 0, lowerStop = 0;
                    var useLower = false;
                    for (var i = 0; i < result.Length; i++)
                    {
                        var price = bars[i].Close;
                        var gain = Math.Pow(efficiency[i], stopPower);
                        var upper = (1 - gain) * Math.Max(price, upperEnvelope) + gain * Math.Min(price, upperEnvelope);
                        var lower = (1 - gain) * Math.Min(price, lowerEnvelope) + gain * Math.Max(price, lowerEnvelope);
                        var upperDirection = Math.Sign(upper - upperEnvelope);
                        var lowerDirection = Math.Sign(lower - lowerEnvelope);
                        if (upperDirection > 0 || upperDirection < 0 && lowerDirection < 0) upperStop = upper;
                        if (lowerDirection < 0 || lowerDirection > 0 && upperDirection > 0) lowerStop = lower;
                        if (upperStop > price) useLower = true;
                        else if (lowerStop > price) useLower = false;
                        result[i] = useLower ? lowerStop : upperStop;
                        upperEnvelope = upper;
                        lowerEnvelope = lower;
                    }
                    return Outputs(("Ts", result));
                });
            case IndicatorName.AutoDispersionBands:
                kind = AverageKind(options, 2);
                var dispersionSmooth = Integer(options, "SmoothLength", 140);
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var changes = bars.Select((b, i) => i < length ? 0 : b.Close - bars[i - length].Close).ToArray();
                    var spread = changes.Select((_, i) => Math.Sqrt(Window(changes, i, length).Average(v => v * v))).ToArray();
                    var upper = bars.Select((b, i) => b.Close + spread[i]).ToArray();
                    var lower = bars.Select((b, i) => b.Close - spread[i]).ToArray();
                    var center = bars.Select((_, i) => (Window(upper, i, length).Max() + Window(lower, i, length).Min()) / 2).ToArray();
                    var upperLine = Average(Average(upper.Select((_, i) => Window(upper, i, length).Max()).ToArray(), length, kind), dispersionSmooth, kind);
                    var lowerLine = Average(Average(lower.Select((_, i) => Window(lower, i, length).Min()).ToArray(), length, kind), dispersionSmooth, kind);
                    return Outputs(("MiddleBand", Average(Average(center, length, kind), dispersionSmooth, kind)),
                        ("UpperBand", upperLine), ("LowerBand", lowerLine));
                });
            case IndicatorName.AdaptivePriceZoneIndicator:
                var zonePeriod = Math.Max(2, Math.Min(530, (int)Math.Ceiling(Math.Sqrt(length))));
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var center = Average(Average(Closes(bars), zonePeriod, kind), zonePeriod, kind);
                    var range = bars.Select(b => b.High - b.Low).ToArray();
                    var width = Average(Average(range, zonePeriod, kind), zonePeriod, kind);
                    var multiplier = Number(options, 2, "Pct");
                    return Outputs(("MiddleBand", center),
                        ("UpperBand", center.Select((v, i) => v + multiplier * width[i]).ToArray()),
                        ("LowerBand", center.Select((v, i) => v - multiplier * width[i]).ToArray()));
                });
            case IndicatorName.ChandelierExit:
                kind = AverageKind(options, 6);
                var chandelierMultiplier = Number(options, 3, "Mult");
                return new("ExitLong", new[] { "ExitLong", "ExitShort" }, bars =>
                {
                    var distance = Average(TrueRanges(bars), length, kind).Select(v => v * chandelierMultiplier).ToArray();
                    return Outputs(("ExitLong", bars.Select((_, i) => Window(bars, i, length).Max(b => b.High) - distance[i]).ToArray()),
                        ("ExitShort", bars.Select((_, i) => Window(bars, i, length).Min(b => b.Low) + distance[i]).ToArray()));
                });
            case IndicatorName.TFSTetherLineIndicator:
                return new("Tether", new[] { "Tether" }, bars => Outputs(("Tether", bars.Select((_, i) =>
                    ExactPriceMean(Window(bars, i, length).Max(b => b.High), Window(bars, i, length).Min(b => b.Low))).ToArray())));
            case IndicatorName.PriceChannel:
                var channelPercent = Number(options, .06, "Pct");
                return new("MiddleChannel", new[] { "MiddleChannel", "UpperChannel", "LowerChannel" }, bars =>
                {
                    var center = Average(Closes(bars), length, kind);
                    return Outputs(("MiddleChannel", center), ("UpperChannel", center.Select(v => v * (1 + channelPercent)).ToArray()),
                        ("LowerChannel", center.Select(v => v * (1 - channelPercent)).ToArray()));
                });
            case IndicatorName.MovingAverageChannel:
                kind = AverageKind(options, 1);
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars => Outputs(
                    ("MiddleBand", Average(bars.Select(b => (b.High + b.Low) / 2).ToArray(), length, kind)),
                    ("UpperBand", Average(bars.Select(b => b.High).ToArray(), length, kind)),
                    ("LowerBand", Average(bars.Select(b => b.Low).ToArray(), length, kind))));
            case IndicatorName.KirshenbaumBands:
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var prices = Closes(bars);
                    var period = Integer(options, "Length2", 20);
                    var center = Average(prices, Integer(options, "Length1", 30), kind);
                    var fitted = RegressionEndpoints(prices, period);
                    var errors = fitted.Zip(prices, (f, p) => (f - p) * (f - p)).ToArray();
                    var factor = Number(options, 1, "StdDevFactor");
                    var width = errors.Select((_, i) => factor * Math.Sqrt(Window(errors, i, period).Average())).ToArray();
                    return Outputs(("MiddleBand", center),
                        ("UpperBand", center.Select((v, i) => v + width[i]).ToArray()),
                        ("LowerBand", center.Select((v, i) => v - width[i]).ToArray()));
                });
            case IndicatorName.AverageTrueRangeChannel:
                kind = AverageKind(options, 1);
                var roundedMultiplier = Number(options, 2, "Multiplier");
                return new("UpperBand", new[] { "UpperBand", "MiddleBand", "LowerBand", "Sma" }, bars =>
                {
                    var width = Average(TrueRanges(bars), length, kind).Select(v => v * roundedMultiplier).ToArray();
                    // This channel publishes integer-rounded price +/- ATR boundaries.
                    var upper = bars.Select((b, i) => Math.Round(b.Close + width[i])).ToArray();
                    var lower = bars.Select((b, i) => Math.Round(b.Close - width[i])).ToArray();
                    return Outputs(("UpperBand", upper), ("LowerBand", lower), ("Sma", Average(Closes(bars), length, kind)),
                        ("MiddleBand", upper.Select((v, i) => (v + lower[i]) / 2).ToArray()));
                });
            case IndicatorName.AtrChannelWidth:
                var widthMultiplier = Number(options, 2, "Multiplier");
                return new("Acw", new[] { "Acw" }, bars => Outputs(("Acw",
                    Average(TrueRanges(bars), length, 6).Select(v => 2 * widthMultiplier * v).ToArray())));
            case IndicatorName.StandardDeviationChannel:
                var deviationMultiplier = Number(options, 2, "StdDevMult");
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var prices = Closes(bars);
                    var fit = RegressionEndpoints(prices, length);
                    var variance = PopulationVariance(prices, length);
                    return Outputs(("MiddleBand", fit),
                        ("UpperBand", fit.Select((v, i) => v + deviationMultiplier * Math.Sqrt(variance[i])).ToArray()),
                        ("LowerBand", fit.Select((v, i) => v - deviationMultiplier * Math.Sqrt(variance[i])).ToArray()));
                });
            case IndicatorName.StollerAverageRangeChannels:
                kind = AverageKind(options, 1);
                var atrMultiplier = Number(options, 2, "AtrMult");
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var center = Average(Closes(bars), length, kind);
                    var ranges = Average(TrueRanges(bars), length, kind);
                    return Outputs(("MiddleBand", center),
                        ("UpperBand", center.Select((v, i) => v + atrMultiplier * ranges[i]).ToArray()),
                        ("LowerBand", center.Select((v, i) => v - atrMultiplier * ranges[i]).ToArray()));
                });
            case IndicatorName.HighLowBands:
                kind = AverageKind(options, 1);
                var shift = Number(options, 1, "PctShift") / 100;
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var center = Average(Average(Closes(bars), length, kind), length, kind);
                    return Outputs(("MiddleBand", center), ("UpperBand", center.Select(v => v * (1 + shift)).ToArray()),
                        ("LowerBand", center.Select(v => v * (1 - shift)).ToArray()));
                });
            case IndicatorName.HighLowMovingAverage:
                kind = AverageKind(options, 2);
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var high = Average(bars.Select((_, i) => Window(bars, i, length).Max(b => b.High)).ToArray(), length, kind);
                    var low = Average(bars.Select((_, i) => Window(bars, i, length).Min(b => b.Low)).ToArray(), length, kind);
                    return Outputs(("MiddleBand", high.Select((v, i) => (v + low[i]) / 2).ToArray()),
                        ("UpperBand", high), ("LowerBand", low));
                });
            case IndicatorName.IchimokuChikouSpan:
                return new("ChikouSpan", new[] { "ChikouSpan" }, bars => Outputs(("ChikouSpan", Closes(bars))));
            case IndicatorName.IchimokuCloud:
                var selected = indicator.BatchOutputKey;
                var tenkanLength = Integer(options, "TenkanLength", selected == "TenkanSen" ? length : 9);
                var kijunLength = Integer(options, "KijunLength", selected is "KijunSen" or "SenkouSpanA" ? length : 26);
                var senkouLength = Integer(options, "SenkouLength", selected == "SenkouSpanB" ? length : 52);
                return new("TenkanSen", new[] { "TenkanSen", "KijunSen", "SenkouSpanA", "SenkouSpanB" }, bars =>
                {
                    double[] Midrange(int period) => bars.Select((_, i) =>
                        ExactPriceMean(Window(bars, i, period).Max(b => b.High), Window(bars, i, period).Min(b => b.Low))).ToArray();
                    var tenkan = Midrange(tenkanLength);
                    var kijun = Midrange(kijunLength);
                    // Published values are the current calculation; chart displacement is external.
                    return Outputs(("TenkanSen", tenkan), ("KijunSen", kijun),
                        ("SenkouSpanA", tenkan.Select((v, i) => ExactPriceMean(v, kijun[i])).ToArray()), ("SenkouSpanB", Midrange(senkouLength)));
                });
            case IndicatorName.AlligatorIndex:
            case IndicatorName.GatorOscillator:
                var leg = indicator.BatchOutputKey;
                var jawLength = Integer(options, "JawLength", name == IndicatorName.GatorOscillator || leg == "Jaws" ? length : 13);
                var teethLength = Integer(options, "TeethLength", leg == "Teeth" ? length : 8);
                var lipsLength = Integer(options, "LipsLength", leg == "Lips" ? length : 5);
                var jawOffset = Integer(options, "JawOffset", 8);
                var teethOffset = Integer(options, "TeethOffset", 5);
                var lipsOffset = Integer(options, "LipsOffset", 3);
                kind = AverageKind(options, 6);
                if (kind == 0) return null;
                return new(name == IndicatorName.AlligatorIndex ? "Jaws" : "Top",
                    new[] { "Jaws", "Teeth", "Lips", "Top", "Bottom" }, bars =>
                    {
                        var median = bars.Select(b => (b.High + b.Low) / 2).ToArray();
                        double[] Line(int period, int delay)
                        {
                            var average = Average(median, period, kind);
                            return average.Select((_, i) => i < delay ? 0 : average[i - delay]).ToArray();
                        }
                        var jaw = Line(jawLength, jawOffset);
                        var teeth = Line(teethLength, teethOffset);
                        var lips = Line(lipsLength, lipsOffset);
                        return Outputs(("Jaws", jaw), ("Teeth", teeth), ("Lips", lips),
                            ("Top", jaw.Select((v, i) => Math.Abs(v - teeth[i])).ToArray()),
                            ("Bottom", teeth.Select((v, i) => -Math.Abs(v - lips[i])).ToArray()));
                    });
            case IndicatorName.Midpoint:
            case IndicatorName.Midprice:
                var midpointKey = name == IndicatorName.Midpoint ? "HCLC2" : "HHLL2";
                return new(midpointKey, new[] { midpointKey }, bars => Outputs((midpointKey, bars.Select((_, i) =>
                {
                    var window = Window(bars, i, length).ToArray();
                    return name == IndicatorName.Midpoint ? ExactPriceMean(window.Max(b => b.Close), window.Min(b => b.Close))
                        : ExactPriceMean(window.Max(b => b.High), window.Min(b => b.Low));
                }).ToArray())));
            case IndicatorName.MovingAverageEnvelope:
                var percent = Number(options, .025, "Pct", "Mult");
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var middle = Average(Closes(bars), length, kind);
                    return Outputs(("UpperBand", middle.Select(v => v * (1 + percent)).ToArray()),
                        ("MiddleBand", middle), ("LowerBand", middle.Select(v => v * (1 - percent)).ToArray()));
                });
            case IndicatorName.KeltnerChannels:
                var centerLength = Integer(options, "Length1", length);
                var atrLength = Integer(options, "Length2", 10);
                var multiplier = Number(options, 2, "MultFactor");
                return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    var middle = Average(Closes(bars), centerLength, kind);
                    var atr = Average(TrueRanges(bars), atrLength, 6);
                    return Outputs(("UpperBand", middle.Select((v, i) => v + multiplier * atr[i]).ToArray()),
                        ("MiddleBand", middle), ("LowerBand", middle.Select((v, i) => v - multiplier * atr[i]).ToArray()));
                });
            case IndicatorName.KeltnerChannelWidth:
                var factor = Number(options, 2, "Multiplier");
                return new("Kcw", new[] { "Kcw" }, bars =>
                {
                    var center = Average(Closes(bars), length, 3);
                    var atr = Average(TrueRanges(bars), length, 6);
                    return Outputs(("Kcw", center.Select((v, i) => v == 0 ? 0 : 200 * factor * atr[i] / v).ToArray()));
                });
            default: return null;
        }
    }
    private static (double[] Upper, double[] Lower) ReferenceProjectionEnvelope(IReadOnlyList<Bar> bars, int period)
    {
        var highs = bars.Select(b => b.High).ToArray();
        var lows = bars.Select(b => b.Low).ToArray();
        double[] Slopes(double[] values) => values.Select((_, i) =>
        {
            var window = Window(values, i, period).ToArray();
            var center = (window.Length - 1d) / 2;
            var mean = window.Average();
            var spread = window.Length * (window.Length * (double)window.Length - 1) / 12;
            return spread == 0 ? 0 : window.Select((v, j) => (v - mean) * (j - center)).Sum() / spread;
        }).ToArray();
        var highSlopes = Slopes(highs);
        var lowSlopes = Slopes(lows);
        // Published convention extrapolates each lagged bar using the preceding fitted slope;
        // unavailable bars and slopes are zero. The current candle remains inside the envelope.
        double[] Envelope(double[] values, double[] slopes, bool upper) => values.Select((v, i) =>
        {
            var projected = Enumerable.Range(1, period).Select(lag => (i < lag - 1 ? 0 : values[i - lag + 1])
                + lag * (i < lag ? 0 : slopes[i - lag])).Append(v);
            return upper ? projected.Max() : projected.Min();
        }).ToArray();
        return (Envelope(highs, highSlopes, true), Envelope(lows, lowSlopes, false));
    }

}
