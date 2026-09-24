using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static double[] SmoothedChannelIndex(double[] prices, int period, int kind, double constant)
    {
        var mean = Average(prices, period, kind);
        var residual = prices.Zip(mean, (p, m) => p - m).ToArray();
        var deviation = Average(residual.Select(Math.Abs).ToArray(), period, kind);
        return residual.Select((r, i) => deviation[i] == 0 ? 0 : r / (constant * deviation[i])).ToArray();
    }

    private static FormulaDefinition? Statistics(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = options is StochasticSpecOptions stochastic ? stochastic.KLength : Integer(options, "Length", 14);
        var name = indicator.BatchName;
        var kind = AverageKind(options, name == IndicatorName.ChandeMomentumOscillator ? 3 : 1);
        switch (name)
        {
            case IndicatorName.ZeroLagSmoothedCycle:
                return new("Filter", new[] { "Lco", "Filter" }, bars =>
                {
                    // Expand [(I-R)(2I-R)]³ as a polynomial in the linear endpoint-regression operator R.
                    var coefficients = new[] { 8d, -36, 66, -63, 33, -9, 1 };
                    var power = Closes(bars);
                    var line = power.Select(v => coefficients[0] * v).ToArray();
                    for (var degree = 1; degree < coefficients.Length; degree++)
                    {
                        power = RegressionEndpoints(power, length);
                        for (var i = 0; i < line.Length; i++) line[i] += coefficients[degree] * power[i];
                    }
                    var smoothLength = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d)));
                    var first = line.Select((_, i) => Window(line, i, smoothLength).Average()).ToArray();
                    var filter = first.Select((_, i) => -2 * Window(first, i, smoothLength).Average()).ToArray();
                    return Outputs(("Lco", line), ("Filter", filter));
                });
            case IndicatorName.KendallRankCorrelationCoefficient:
                return new("Krcc", new[] { "Krcc" }, bars =>
                {
                    var fitted = RegressionEndpoints(Closes(bars), length);
                    var line = bars.Select((_, i) =>
                    {
                        if (length == 1) return 0d;
                        // Sort pairs by price; count ordering agreements in fitted values, ignoring price ties.
                        var pairs = Enumerable.Range(i - length + 1, length)
                            .Select(j => (Price: j < 0 ? 0 : bars[j].Close, Fit: j < 0 ? 0 : fitted[j]))
                            .OrderBy(pair => pair.Price).ToArray();
                        double score = 0;
                        for (var j = 0; j < pairs.Length; j++)
                            score += pairs.Skip(j + 1).Where(p => p.Price != pairs[j].Price)
                                .Sum(p => Math.Sign(p.Fit - pairs[j].Fit));
                        return 2 * score / (length * (length - 1d));
                    }).ToArray();
                    return Outputs(("Krcc", line));
                });
            case IndicatorName.LogisticCorrelation:
                return new("LogCorr", new[] { "LogCorr" }, bars =>
                {
                    var gain = Number(options, 10, "K");
                    var line = bars.Select((_, i) =>
                    {
                        var window = Window(Closes(bars), i, length).ToArray();
                        var n = window.Length;
                        // Centered least-squares slope divided by the price standard deviation.
                        var mean = window.Average();
                        var center = (n - 1d) / 2;
                        var covariance = window.Select((v, j) => (j - center) * (v - mean)).Sum();
                        var spread = window.Sum(v => (v - mean) * (v - mean));
                        var timeSpread = n * (n * (double)n - 1) / 12;
                        var r = spread == 0 || timeSpread == 0 ? 0 : covariance / Math.Sqrt(timeSpread * spread);
                        var exponent = Math.Min(100, -gain * r);
                        return (1 - Math.Tanh(exponent / 2)) / 2;
                    }).ToArray();
                    return Outputs(("LogCorr", line));
                });
            case IndicatorName.JrcFractalDimension:
                if (kind == 0) return null;
                return new("Jrcfd", new[] { "Jrcfd", "Signal" }, bars =>
                {
                    var shortPeriod = Integer(options, "Length1", 20);
                    var scale = Integer(options, "Length2", 5);
                    var aggregation = Math.Max(2, Math.Min(530, (scale - 1) * shortPeriod));
                    var longPeriod = Math.Max(2, Math.Min(530, scale * shortPeriod));
                    double Range(int index, int period)
                    {
                        var window = Window(bars, index, period).ToArray();
                        var previous = index < period ? 0 : bars[index - period].Close;
                        return Math.Max(previous, window.Max(b => b.High)) - Math.Min(previous, window.Min(b => b.Low));
                    }
                    var ranges = bars.Select((_, i) => Range(i, shortPeriod)).ToArray();
                    var dimension = bars.Select((_, i) =>
                    {
                        if (scale == 1) return 0;
                        var mean = Window(ranges, i, aggregation).Sum() / aggregation;
                        var spread = Range(i, longPeriod);
                        return mean <= 0 || spread <= 0 ? 2 : 2 + Math.Log(mean / spread) / Math.Log(scale);
                    }).ToArray();
                    var smoothing = Integer(options, "SmoothLength", 5);
                    var line = Average(dimension, smoothing, kind);
                    return Outputs(("Jrcfd", line), ("Signal", Average(line, smoothing, kind)));
                });
            case IndicatorName.StiffnessIndicator:
                if (kind == 0) return null;
                return new("Si", new[] { "Si" }, bars =>
                {
                    var prices = Closes(bars);
                    var period = Integer(options, "Length1", 100);
                    var lookback = Integer(options, "Length2", 60);
                    var mean = Average(prices, period, kind);
                    var variance = PopulationVariance(prices, period);
                    var above = prices.Select((p, i) => p > mean[i] - Math.Sqrt(variance[i]) / 5).ToArray();
                    var fractions = above.Select((_, i) => 100d * Window(above, i, lookback).Count(v => v) / lookback).ToArray();
                    return Outputs(("Si", Average(fractions, Integer(options, "SmoothingLength", 3), 3)));
                });
            case IndicatorName.SurfaceRoughnessEstimator:
                return new("Sre", new[] { "Sre" }, bars =>
                {
                    var prices = Closes(bars);
                    var line = prices.Select((_, i) =>
                    {
                        var first = Math.Max(0, i - length + 1);
                        var x = Enumerable.Range(first, i - first + 1).Select(j => j == 0 ? 0 : prices[j - 1]).ToArray();
                        var y = Window(prices, i, length).ToArray();
                        var xm = x.Average();
                        var ym = y.Average();
                        var xs = x.Select(v => v - xm).ToArray();
                        var ys = y.Select(v => v - ym).ToArray();
                        var denominator = Math.Sqrt(xs.Sum(v => v * v) * ys.Sum(v => v * v));
                        var correlation = denominator == 0 ? 0 : xs.Zip(ys, (a, b) => a * b).Sum() / denominator;
                        return (1 - correlation) / 2;
                    }).ToArray();
                    return Outputs(("Sre", line));
                });
            case IndicatorName.InertiaIndicator:
                var inertiaKind = AverageKind(options, 0);
                var regression = options.GetType().GetProperty("MaType")?.GetValue(options) is not MovingAvgType inertiaAverage
                    || inertiaAverage == MovingAvgType.LinearRegression;
                if (!regression && inertiaKind == 0) return null;
                return new("Inertia", new[] { "Inertia" }, bars =>
                {
                    var smoothing = Integer(options, "RviLength", 14);
                    var high = RelativeVolatilityTrajectory(bars.Select(b => b.High).ToArray(), 10, smoothing, 6);
                    var low = RelativeVolatilityTrajectory(bars.Select(b => b.Low).ToArray(), 10, smoothing, 6);
                    var index = high.Zip(low, (h, l) => (h + l) / 2).ToArray();
                    var period = Integer(options, "SmoothLength", length);
                    return Outputs(("Inertia", regression ? RegressionEndpoints(index, period) : Average(index, period, inertiaKind)));
                });
            case IndicatorName.RelativeVolatilityIndexV1:
            case IndicatorName.RelativeVolatilityIndexV2:
                var volatilityKind = AverageKind(options, 6);
                if (volatilityKind == 0) return null;
                var volatilitySmooth = Integer(options, "SmoothLength", 14);
                return new("Rvi", new[] { "Rvi" }, bars =>
                {
                    var line = name == IndicatorName.RelativeVolatilityIndexV1
                        ? RelativeVolatilityTrajectory(Closes(bars), length, volatilitySmooth, volatilityKind)
                        : RelativeVolatilityTrajectory(bars.Select(b => b.High).ToArray(), length, volatilitySmooth, volatilityKind)
                            .Zip(RelativeVolatilityTrajectory(bars.Select(b => b.Low).ToArray(), length, volatilitySmooth, volatilityKind), (h, l) => (h + l) / 2).ToArray();
                    return Outputs(("Rvi", line));
                });
            case IndicatorName.RelativeVolatilityIndexHigh:
            case IndicatorName.RelativeVolatilityIndexLow:
                var highSide = name == IndicatorName.RelativeVolatilityIndexHigh;
                var sideKey = highSide ? "RviHigh" : "RviLow";
                return new(sideKey, new[] { sideKey }, bars =>
                {
                    var prices = bars.Select(b => highSide ? b.High : b.Low).ToArray();
                    var deviations = PopulationVariance(prices, Integer(options, "StdDevLength", 10)).Select(Math.Sqrt).ToArray();
                    var signed = prices.Select((value, i) => i == 0 ? 0 : Math.Sign(value - prices[i - 1]) * deviations[i]).ToArray();
                    var line = prices.Select((_, i) =>
                    {
                        if (i < length) return 0;
                        double net = 0, total = 0;
                        // Wilder's mean seed on bars 1..length, followed by geometric observation weights.
                        for (var j = 1; j <= i; j++)
                        {
                            var weight = Math.Pow(1 - 1d / length, i - Math.Max(length, j));
                            net += weight * signed[j];
                            total += weight * Math.Abs(signed[j]);
                        }
                        return total == 0 ? 50 : 50 * (1 + net / total);
                    }).ToArray();
                    return Outputs((sideKey, line));
                });
            case IndicatorName.ShinoharaIntensityRatio:
                return new("ARatio", new[] { "ARatio", "BRatio" }, bars =>
                {
                    double[] Ratio(bool previousClose)
                    {
                        var upside = bars.Select((b, i) => b.High - (previousClose ? i == 0 ? 0 : bars[i - 1].Close : b.Open)).ToArray();
                        var downside = bars.Select((b, i) => (previousClose ? i == 0 ? 0 : bars[i - 1].Close : b.Open) - b.Low).ToArray();
                        return bars.Select((_, i) =>
                        {
                            var denominator = Window(downside, i, length).Sum();
                            return denominator == 0 ? 0 : 100 * Window(upside, i, length).Sum() / denominator;
                        }).ToArray();
                    }
                    return Outputs(("ARatio", Ratio(false)), ("BRatio", Ratio(true)));
                });
            case IndicatorName.VerticalHorizontalFilter:
                kind = AverageKind(options, 2);
                if (kind == 0) return null;
                return new("Vhf", new[] { "Vhf", "Signal" }, bars =>
                {
                    var prices = Closes(bars);
                    var travel = prices.Select((value, i) => i == 0 ? 0 : Math.Abs(value - prices[i - 1])).ToArray();
                    var line = prices.Select((_, i) =>
                    {
                        var window = Window(prices, i, length).ToArray();
                        var distance = Window(travel, i, length).Sum();
                        return distance == 0 ? 0 : (window.Max() - window.Min()) / distance;
                    }).ToArray();
                    return Outputs(("Vhf", line), ("Signal", Average(line, 6, kind)));
                });
            case IndicatorName.MarketMeannessIndex:
                var meannessAverage = (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!;
                if (kind == 0 && meannessAverage != MovingAvgType.EhlersNoiseEliminationTechnology) return null;
                return new("Mmi", new[] { "Mmi", "MmiSmoothed" }, bars =>
                {
                    var prices = Closes(bars);
                    var line = prices.Select((_, i) =>
                    {
                        if (length == 1) return 0d;
                        var ordered = Window(prices, i, length).OrderBy(value => value).ToArray();
                        var median = (ordered[(ordered.Length - 1) / 2] + ordered[ordered.Length / 2]) / 2;
                        // Count moves away from the window median, among length-1 adjacent pairs.
                        var pairs = Enumerable.Range(0, length - 1).Select(lag =>
                            (Current: i >= lag ? prices[i - lag] : 0, Previous: i > lag ? prices[i - lag - 1] : 0));
                        var outward = pairs.Count(pair => (pair.Current > median && pair.Current > pair.Previous)
                            || (pair.Current < median && pair.Current < pair.Previous));
                        return 100d * outward / (length - 1);
                    }).ToArray();
                    var smoothed = kind != 0 ? Average(line, length, kind) : KendallTrajectory(line, length);
                    return Outputs(("Mmi", line), ("MmiSmoothed", smoothed));
                });
            case IndicatorName.ChandeKrollRSquaredIndex:
                if (kind == 0) return null;
                return new("Ckrsi", new[] { "Ckrsi" }, bars =>
                {
                    var raw = bars.Select((_, i) =>
                    {
                        var prices = Window(bars, i, length).Select(b => b.Close).ToArray();
                        if (prices.Length < 2) return 0d;
                        var mean = prices[0] + prices.Average(v => v - prices[0]);
                        var center = (prices.Length - 1) / 2d;
                        var covariance = prices.Select((v, j) => (v - mean) * (j - center)).Sum();
                        var priceVariance = prices.Sum(v => (v - mean) * (v - mean));
                        var timeVariance = prices.Length * (prices.Length * (double)prices.Length - 1) / 12;
                        return priceVariance == 0 ? 0 : Clamp(covariance * covariance / (priceVariance * timeVariance), 0, 1);
                    }).ToArray();
                    return Outputs(("Ckrsi", Average(raw, 3, kind)));
                });
            case IndicatorName.AutoLine:
            case IndicatorName.AutoLineWithDrift:
            case IndicatorName.AutoFilter:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                var drift = name == IndicatorName.AutoLineWithDrift;
                var autoKey = name == IndicatorName.AutoFilter ? "Af" : drift ? "Alwd" : "Al";
                return new(autoKey, new[] { autoKey }, bars =>
                {
                    var prices = Closes(bars);
                    var variance = PopulationVariance(prices, length);
                    var held = new double[bars.Count];
                    for (var i = 0; i < held.Length; i++)
                    {
                        var initial = drift ? Math.Round(prices[i]) : prices[i];
                        var previous = i == 0 ? initial : held[i - 1];
                        var escaped = Math.Pow(prices[i] - previous, 2) > variance[i];
                        var anchor = i <= length ? initial : held[i - length - 1];
                        held[i] = escaped ? prices[i] : previous + (drift ? (previous - anchor) / (2 * length) : 0);
                    }
                    if (name != IndicatorName.AutoFilter) return Outputs((autoKey, held));
                    var meanPrice = Average(prices, length, kind);
                    var meanHeld = Average(held, length, kind);
                    return Outputs((autoKey, held.Select((v, i) =>
                    {
                        if (i + 1 < length) return meanPrice[i];
                        var x = Window(held, i, length).ToArray();
                        var y = Window(prices, i, length).ToArray();
                        var mx = x.Average();
                        var my = y.Average();
                        var xx = x.Sum(z => (z - mx) * (z - mx));
                        var xy = x.Select((z, j) => (z - mx) * (y[j] - my)).Sum();
                        return meanPrice[i] + (xx == 0 ? 0 : xy / xx * (v - meanHeld[i]));
                    }).ToArray()));
                });
            case IndicatorName.StatisticalVolatility:
                kind = AverageKind(options, 3);
                if (kind == 0) return null;
                var rangePeriod = Integer(options, "Length1", 30);
                var annualPeriod = Integer(options, "Length2", 253);
                return new("Sv", new[] { "Sv", "Signal" }, bars =>
                {
                    var line = bars.Select((_, i) =>
                    {
                        var window = Window(bars, i, rangePeriod).ToArray();
                        var minClose = window.Min(b => b.Close);
                        var minLow = window.Min(b => b.Low);
                        var closeRange = minClose == 0 ? 0 : Math.Log(window.Max(b => b.Close) / minClose);
                        var wickRange = minLow == 0 ? 0 : Math.Log(window.Max(b => b.High) / minLow);
                        return Math.Max(0, Math.Min(2.99, .3 * Math.Sqrt(annualPeriod / (double)rangePeriod) * (closeRange + wickRange)));
                    }).ToArray();
                    return Outputs(("Sv", line), ("Signal", Average(line, rangePeriod, kind)));
                });
            case IndicatorName.LinearRegressionLine:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("LinReg", new[] { "LinReg" }, bars =>
                {
                    var prices = Closes(bars);
                    var meanPrice = Average(prices, length, kind);
                    var meanTime = Average(Enumerable.Range(0, bars.Count).Select(i => (double)i).ToArray(), length, kind);
                    return Outputs(("LinReg", bars.Select((_, i) =>
                    {
                        if (i + 1 < length || length == 1) return meanPrice[i];
                        var window = Window(prices, i, length).ToArray();
                        var center = (length - 1d) / 2;
                        var mean = window.Average();
                        var slope = window.Select((v, j) => (j - center) * (v - mean)).Sum()
                            / Enumerable.Range(0, length).Sum(j => Math.Pow(j - center, 2));
                        return meanPrice[i] + slope * (i - meanTime[i]);
                    }).ToArray()));
                });
            case IndicatorName.LinearExtrapolation:
                return new("LinExt", new[] { "LinExt" }, bars => Outputs(("LinExt", bars.Select((_, i) =>
                    i < length ? 0 : i == length ? bars[0].Close
                        : i < 2 * length ? bars[i - length].Close * i / (i - length)
                        : 2 * bars[i - length].Close - bars[i - 2 * length].Close).ToArray())));
            case IndicatorName.PercentRank:
                return new("PercentRank", new[] { "PercentRank" }, bars => Outputs(("PercentRank", bars.Select((b, i) =>
                {
                    if (i < length) return 0d;
                    // Empirical CDF's left limit: exclude today's sample and all ties.
                    var ordered = bars.Skip(i - length).Take(length).Select(v => v.Close).OrderBy(v => v).ToArray();
                    return 100d * Array.FindIndex(ordered.Concat(new[] { double.PositiveInfinity }).ToArray(), v => v >= b.Close) / length;
                }).ToArray())));
            case IndicatorName.GopalakrishnanRangeIndex:
                kind = AverageKind(options, 2);
                if (kind == 0) return null;
                return new("Gapo", new[] { "Gapo", "Signal" }, bars =>
                {
                    var line = bars.Select((_, i) =>
                    {
                        var window = Window(bars, i, length).ToArray();
                        var range = window.Max(b => b.High) - window.Min(b => b.Low);
                        return range <= 0 ? 0 : Math.Log(range, length);
                    }).ToArray();
                    return Outputs(("Gapo", line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.AverageDayRange:
                return new("Adr", new[] { "Adr" }, bars => Outputs(("Adr",
                    Average(bars.Select(b => b.High - b.Low).ToArray(), length, 1))));
            case IndicatorName.Trimean:
                return new("Trimean", new[] { "Trimean", "Q1", "Median", "Q3" }, bars =>
                {
                    var quartiles = new[] { new double[bars.Count], new double[bars.Count], new double[bars.Count] };
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var ordered = Window(bars, i, length).Select(b => b.Close).OrderBy(v => v).ToArray();
                        for (var q = 1; q <= 3; q++)
                            quartiles[q - 1][i] = ordered[(q * ordered.Length + 3) / 4 - 1];
                    }
                    return Outputs(("Q1", quartiles[0]), ("Median", quartiles[1]), ("Q3", quartiles[2]),
                        ("Trimean", quartiles[1].Select((v, i) => ExactPriceMean(quartiles[0][i], v, v, quartiles[2][i])).ToArray()));
                });
            case IndicatorName.MedianValue:
                return new("MedianValue", new[] { "MedianValue" }, bars => Outputs(("MedianValue", bars.Select((b, i) =>
                {
                    if (i + 1 < length) return b.Close;
                    var ordered = Window(bars, i, length).Select(v => v.Close).OrderBy(v => v).ToArray();
                    return ExactPriceMean(ordered[(length - 1) / 2], ordered[length / 2]);
                }).ToArray())));
            case IndicatorName.Skewness:
                return new("Skewness", new[] { "Skewness" }, bars => Outputs(("Skewness", bars.Select((_, i) =>
                {
                    if (i + 1 < length) return 0;
                    var values = Window(bars, i, length).Select(b => b.Close).ToArray();
                    var mean = values[0] + values.Average(v => v - values[0]);
                    var deviations = values.Select(v => v - mean).ToArray();
                    var scale = deviations.Max(v => Math.Abs(v));
                    if (scale == 0) return 0;
                    var normalized = deviations.Select(v => v / scale).ToArray();
                    return normalized.Average(v => v * v * v) / Math.Pow(normalized.Average(v => v * v), 1.5);
                }).ToArray())));
            case IndicatorName.TypicalPriceVolatility:
                return new("Tpv", new[] { "Tpv" }, bars => Outputs(("Tpv",
                    PopulationVariance(bars.Select(b => (b.High + b.Low + b.Close) / 3).ToArray(), length)
                        .Select(Math.Sqrt).ToArray())));
            case IndicatorName.CoefficientOfVariation:
                return new("Cv", new[] { "Cv" }, bars =>
                {
                    var prices = Closes(bars);
                    var variance = PopulationVariance(prices, length);
                    var mean = Average(prices, length, 1);
                    return Outputs(("Cv", mean.Select((v, i) => v == 0 ? 0 : 100 * Math.Sqrt(variance[i]) / v).ToArray()));
                });
            case IndicatorName.DownsideDeviation:
                var target = Number(options, 0, "TargetReturn");
                return new("Dd", new[] { "Dd" }, bars => Outputs(("Dd", bars.Select((_, i) =>
                {
                    if (i < length) return 0;
                    var shortfalls = Enumerable.Range(i - length + 1, length)
                        .Select(j => (bars[j - 1].Close > 0 ? bars[j].Close / bars[j - 1].Close - 1 : 0) - target)
                        .Where(v => v < 0).ToArray();
                    return shortfalls.Length == 0 ? 0 : Math.Sqrt(shortfalls.Average(v => v * v));
                }).ToArray())));
            case IndicatorName.CloseToCloseVolatility:
                return new("Ctcv", new[] { "Ctcv" }, bars =>
                {
                    var returns = bars.Select((b, i) => i == 0 ? 0 : LogRatio(b.Close, bars[i - 1].Close)).ToArray();
                    return Outputs(("Ctcv", PopulationVariance(returns, length).Select(v => Math.Sqrt(252 * v)).ToArray()));
                });
            case IndicatorName.ChandeForecastOscillator:
                return new("Cfo", new[] { "Cfo" }, bars =>
                {
                    var fit = RegressionEndpoints(Closes(bars), length);
                    return Outputs(("Cfo", bars.Select((b, i) => b.Close == 0 ? 0 : 100 * (b.Close - fit[i]) / b.Close).ToArray()));
                });
            case IndicatorName.StochasticRelativeStrengthIndex:
                kind = AverageKind(options, 6);
                if (kind == 0) return null;
                var rsiLength = Integer(options, "RsiLength", length);
                var stochLength = Integer(options, "StochLength", rsiLength);
                var firstSmooth = Integer(options, "SmoothLength1", 3);
                var secondSmooth = Integer(options, "SmoothLength2", 3);
                return new("StochRsi", new[] { "StochRsi", "Signal" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var up = Average(changes.Select(v => Math.Max(0, v)).ToArray(), rsiLength, kind);
                    var down = Average(changes.Select(v => Math.Max(0, -v)).ToArray(), rsiLength, kind);
                    var rsi = up.Select((v, i) => down[i] == 0 ? 100 : 100 * v / (v + down[i])).ToArray();
                    // Equal geometric decay cancels in the ratio. Preserve this identity
                    // exactly so an extrema calculation cannot amplify roundoff into a signal.
                    if (rsiLength > 1 && (kind == 3 || kind == 6))
                        for (var i = 1; i < rsi.Length; i++)
                            if (changes[i] == 0) rsi[i] = rsi[i - 1];
                    var raw = rsi.Select((v, i) =>
                    {
                        var window = Window(rsi, i, stochLength).ToArray();
                        var minimum = window.Min();
                        var maximum = window.Max();
                        return maximum == minimum ? 0 : 100 * (v - minimum) / (maximum - minimum);
                    }).ToArray();
                    var line = Average(raw, firstSmooth, kind);
                    return Outputs(("StochRsi", line), ("Signal", Average(line, secondSmooth, kind)));
                });
            case IndicatorName.StandardError:
                return new("StandardError", new[] { "StandardError" }, bars => Outputs(("StandardError", bars.Select((_, i) =>
                {
                    if (i + 1 < length) return 0;
                    var prices = Window(bars, i, length).Select(b => b.Close).ToArray();
                    var center = (length - 1) / 2d;
                    var mean = prices[0] + prices.Average(v => v - prices[0]);
                    var xx = Enumerable.Range(0, length).Sum(j => (j - center) * (j - center));
                    var slope = xx == 0 ? 0 : prices.Select((v, j) => (v - mean) * (j - center)).Sum() / xx;
                    return Math.Sqrt(prices.Select((v, j) => Math.Pow(v - mean - slope * (j - center), 2)).Average());
                }).ToArray())));
            case IndicatorName.StandardErrorOfTheMean:
                return new("Sem", new[] { "Sem" }, bars => Outputs(("Sem",
                    PopulationVariance(Closes(bars), length).Select(v => Math.Sqrt(v / length)).ToArray())));
            case IndicatorName.FastZScore:
            case IndicatorName.InverseFisherFastZScore:
                if (kind == 0) return null;
                var fastKey = name == IndicatorName.FastZScore ? "Fzs" : "Iffzs";
                return new(fastKey, new[] { fastKey }, bars =>
                {
                    var average = Average(Closes(bars), length, kind);
                    var longFit = RegressionEndpoints(average, length);
                    var shortFit = RegressionEndpoints(average, Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d))));
                    var variance = PopulationVariance(average, length);
                    return Outputs((fastKey, average.Select((_, i) =>
                    {
                        var z = variance[i] == 0 ? 0 : (shortFit[i] - longFit[i]) / (2 * Math.Sqrt(variance[i]));
                        return name == IndicatorName.FastZScore ? z : Math.Tanh(5 * z);
                    }).ToArray()));
                });
            case IndicatorName.InverseFisherZScore:
            case IndicatorName.ZScore:
                if (kind == 0) return null;
                var scoreKey = name == IndicatorName.ZScore ? "Zscore" : "Ifzs";
                return new(scoreKey, new[] { scoreKey }, bars =>
                {
                    var prices = Closes(bars);
                    var mean = Average(prices, length, kind);
                    var variance = PopulationVariance(prices, length);
                    return Outputs((scoreKey, prices.Select((v, i) =>
                    {
                        var z = variance[i] == 0 ? 0 : (v - mean[i]) / Math.Sqrt(variance[i]);
                        return name == IndicatorName.ZScore ? z : 50 * (1 + Math.Tanh(z));
                    }).ToArray()));
                });
            case IndicatorName.UlcerIndex:
                return new("Ui", new[] { "Ui" }, bars =>
                {
                    var squaredDrawdown = bars.Select((b, i) =>
                    {
                        var peak = Window(bars, i, length).Max(v => v.Close);
                        return peak == 0 ? 0 : Math.Pow(100 * (b.Close / peak - 1), 2);
                    }).ToArray();
                    return Outputs(("Ui", squaredDrawdown.Select((_, i) => Math.Sqrt(Window(squaredDrawdown, i, length).Average())).ToArray()));
                });
            case IndicatorName.ChoppinessIndex:
                if (length <= 1) return null; // log(length) has no defined denominator at period one.
                return new("Ci", new[] { "Ci" }, bars =>
                {
                    var ranges = TrueRanges(bars);
                    return Outputs(("Ci", bars.Select((_, i) =>
                    {
                        var window = Window(bars, i, length).ToArray();
                        var span = window.Max(b => b.High) - window.Min(b => b.Low);
                        return span == 0 ? 0 : 100 * Math.Log(Window(ranges, i, length).Sum() / span) / Math.Log(length);
                    }).ToArray()));
                });
            case IndicatorName.ChaikinVolatility:
                kind = AverageKind(options, 3);
                if (kind == 0) return null;
                var smoothing = Integer(options, "Length1", length);
                var lag = Integer(options, "Length2", 12);
                return new("Cv", new[] { "Cv" }, bars =>
                {
                    var average = Average(bars.Select(b => b.High - b.Low).ToArray(), smoothing, kind);
                    return Outputs(("Cv", average.Select((v, i) => i < lag || average[i - lag] == 0 ? 0 : 100 * (v / average[i - lag] - 1)).ToArray()));
                });
            case IndicatorName.WilliamsR:
            case IndicatorName.StochasticOscillator:
            case IndicatorName.StochasticFastOscillator:
            case IndicatorName.StochasticRegular:
                if (name == IndicatorName.StochasticFastOscillator) kind = AverageKind(options, 3);
                if (name == IndicatorName.StochasticRegular) length = Integer(options, "Length1", 5);
                var smooth1 = options is StochasticSpecOptions first ? first.DLength : Integer(options, "SmoothLength1", 3);
                if (name == IndicatorName.StochasticRegular) smooth1 = Integer(options, "Length2", 3);
                var smooth2 = Integer(options, "SmoothLength2", name == IndicatorName.StochasticFastOscillator ? 2 : 3);
                if (kind == 0 || kind is >= 3 and <= 5 && (!StandardEmaPeriod(smooth1) || !StandardEmaPeriod(smooth2))) return null;
                var key = name switch { IndicatorName.WilliamsR => "Williams%R", IndicatorName.StochasticFastOscillator => "Sfo",
                    IndicatorName.StochasticRegular => "Sco", _ => "FastK" };
                var keys = name == IndicatorName.WilliamsR ? new[] { key } : name == IndicatorName.StochasticOscillator
                    ? new[] { "FastK", "FastD", "SlowD" } : new[] { key, "Signal" };
                return new(key, keys, bars =>
                {
                    var position = bars.Select((bar, i) =>
                    {
                        var window = Window(bars, i, length).ToArray();
                        var low = window.Min(b => b.Low);
                        var high = window.Max(b => b.High);
                        return high == low ? 0 : 100 * (bar.Close - low) / (high - low);
                    }).ToArray();
                    if (name == IndicatorName.WilliamsR)
                        return Outputs((key, position.Select(v => v - 100).ToArray()));
                    var fastD = Average(position, smooth1, kind);
                    if (name == IndicatorName.StochasticRegular) return Outputs((key, position), ("Signal", fastD));
                    if (name == IndicatorName.StochasticFastOscillator) return Outputs((key, fastD), ("Signal", Average(fastD, smooth2, kind)));
                    return Outputs(("FastK", position), ("FastD", fastD), ("SlowD", Average(fastD, smooth2, kind)));
                });
            case IndicatorName.CommodityChannelIndex:
                if (kind == 0) return null;
                var constant = Number(options, .015, "Constant");
                if (kind != 1)
                    return new("Cci", new[] { "Cci" }, bars => Outputs(("Cci", SmoothedChannelIndex(
                        bars.Select(b => (b.High + b.Low + b.Close) / 3).ToArray(), length, kind, constant))));
                return new("Cci", new[] { "Cci" }, bars => Outputs(("Cci", bars.Select((_, i) =>
                {
                    if (i + 1 < length) return 0;
                    var typical = Window(bars, i, length).Select(b => (b.High + b.Low + b.Close) / 3).ToArray();
                    var mean = typical[0] + typical.Average(v => v - typical[0]);
                    var deviation = typical.Average(v => Math.Abs(v - mean));
                    return deviation == 0 ? 0 : (typical[typical.Length - 1] - mean) / (constant * deviation);
                }).ToArray())));
            case IndicatorName.WoodieCommodityChannelIndex:
                if (kind == 0) return null;
                return new("FastCci", new[] { "FastCci", "SlowCci", "Histogram" }, bars =>
                {
                    var prices = bars.Select(b => (b.High + b.Low + b.Close) / 3).ToArray();
                    double[] Channel(int period) => kind != 1 ? SmoothedChannelIndex(prices, period, kind, .015)
                        : prices.Select((p, i) =>
                        {
                            if (i + 1 < period) return 0;
                            var window = Window(prices, i, period).ToArray();
                            var mean = window[0] + window.Average(v => v - window[0]);
                            var deviation = window.Average(v => Math.Abs(v - mean));
                            return deviation == 0 ? 0 : (p - mean) / (.015 * deviation);
                        }).ToArray();
                    var fast = Channel(Integer(options, "FastLength", 6));
                    var slow = Channel(Integer(options, "SlowLength", 14));
                    return Outputs(("FastCci", fast), ("SlowCci", slow), ("Histogram", fast.Zip(slow, (f, s) => f - s).ToArray()));
                });
            case IndicatorName.ChandeMomentumOscillator:
                var signalLength = Integer(options, "SignalLength", 3);
                if (kind == 0 || kind is >= 3 and <= 5 && !StandardEmaPeriod(signalLength)) return null;
                return new("Cmo", new[] { "Cmo", "Signal" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var line = changes.Select((_, i) =>
                    {
                        var window = Window(changes, i, length).ToArray();
                        var absolute = window.Sum(Math.Abs);
                        return absolute == 0 ? 0 : 100 * window.Sum() / absolute;
                    }).ToArray();
                    return Outputs(("Cmo", line), ("Signal", Average(line, signalLength, kind)));
                });
            case IndicatorName.LinearRegression:
            case IndicatorName.RSquared:
                return new(name == IndicatorName.LinearRegression ? "LinearRegression" : "RSquared",
                    name == IndicatorName.LinearRegression ? new[] { "LinearRegression", "PredictedTomorrow", "Slope", "Intercept" }
                        : new[] { "RSquared" }, bars =>
                    {
                        var fit = new double[bars.Count];
                        var next = new double[bars.Count];
                        var slope = new double[bars.Count];
                        var intercept = new double[bars.Count];
                        var squared = new double[bars.Count];
                        for (var i = 0; i < bars.Count; i++)
                        {
                            var window = Window(bars, i, length).Select(b => b.Close).ToArray();
                            var center = (window.Length - 1) / 2d;
                            var mean = window.Average();
                            var xx = Enumerable.Range(0, window.Length).Sum(j => (j - center) * (j - center));
                            var xy = window.Select((v, j) => (j - center) * (v - mean)).Sum();
                            var yy = window.Sum(v => (v - mean) * (v - mean));
                            slope[i] = xx == 0 ? 0 : xy / xx;
                            fit[i] = mean + slope[i] * center;
                            next[i] = fit[i] + slope[i];
                            intercept[i] = fit[i] - slope[i] * i;
                            squared[i] = window.Length < length || xx * yy == 0 ? 0 : xy * xy / (xx * yy);
                        }
                        return Outputs(("LinearRegression", fit), ("PredictedTomorrow", next),
                            ("Slope", slope), ("Intercept", intercept), ("RSquared", squared));
                    });
            default: return null;
        }
    }

    private static double[] RelativeVolatilityTrajectory(double[] prices, int period, int smoothing, int kind)
    {
        var deviations = PopulationVariance(prices, period).Select(Math.Sqrt).ToArray();
        var signed = prices.Select((value, i) => Math.Sign(value - (i == 0 ? 0 : prices[i - 1])) * deviations[i]).ToArray();
        var magnitude = Average(signed.Select(Math.Abs).ToArray(), smoothing, kind);
        var net = Average(signed, smoothing, kind);
        // Linearity: smooth(up-down) / smooth(up+down) gives the same directional share.
        return net.Select((value, i) => magnitude[i] == 0 ? 100 : 50 * (1 + value / magnitude[i])).ToArray();
    }

    private static double[] RegressionEndpoints(IReadOnlyList<double> values, int length) => values.Select((_, i) =>
    {
        var window = Window(values, i, length).ToArray();
        var center = (window.Length - 1) / 2d;
        var mean = window[0] + window.Average(v => v - window[0]);
        var xx = Enumerable.Range(0, window.Length).Sum(j => (j - center) * (j - center));
        var xy = window.Select((v, j) => (v - mean) * (j - center)).Sum();
        return mean + (xx == 0 ? 0 : center * xy / xx);
    }).ToArray();
    private static double[] KendallTrajectory(double[] values, int length) => values.Select((_, i) =>
    {
        if (length == 1) return 0d;
        // Insert chronological observations into sorted order; insertion ranks count concordant
        // and discordant pairs without the production pairwise sign loop.
        var ordered = new List<double>(length);
        double signedPairs = 0;
        for (var j = i - length + 1; j <= i; j++)
        {
            var value = j < 0 ? 0 : values[j];
            var found = ordered.BinarySearch(value);
            var lower = found < 0 ? ~found : found;
            var upper = lower;
            if (found >= 0)
            {
                while (lower > 0 && ordered[lower - 1] == value) lower--;
                while (upper < ordered.Count && ordered[upper] == value) upper++;
            }
            signedPairs += lower - (ordered.Count - upper);
            ordered.Insert(lower, value);
        }
        return 2 * signedPairs / (length * (length - 1d));
    }).ToArray();

}
