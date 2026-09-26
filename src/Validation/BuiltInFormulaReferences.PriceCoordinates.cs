using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? PriceCoordinates(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var kind = AverageKind(options, 1);
        var mcNicholl = indicator.BatchName == IndicatorName.VortexBands &&
            options.GetType().GetProperty("MaType")?.GetValue(options) is MovingAvgType.McNichollMovingAverage;
        if (kind == 0 && !mcNicholl) return null;
        var length = Integer(options, "Length", 5);
        switch (indicator.BatchName)
        {
            case IndicatorName.PeriodicChannel:
                return new("K", new[] { "K", "Os", "Ap", "Bp", "Cp", "Al", "Bl", "Cl" }, bars =>
                {
                    var prices = Closes(bars);
                    var period = Integer(options, "Length1", 500);
                    var lookback = Integer(options, "Length2", 2);
                    var correlations = prices.Select((_, i) =>
                    {
                        var sample = Window(prices, i, lookback).ToArray();
                        if (sample.Length < 2 || sample.Max() == sample.Min()) return 0d; // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                        if (sample.Length == 2) return (double)Math.Sign(sample[1] - sample[0]);
                        var average = sample.Average();
                        var centeredIndex = Enumerable.Range(0, sample.Length).Select(j => j - (sample.Length - 1d) / 2).ToArray();
                        var covariance = sample.Select((v, j) => (v - average) * centeredIndex[j]).Sum();
                        return covariance / Math.Sqrt(sample.Sum(v => (v - average) * (v - average)) * centeredIndex.Sum(v => v * v));
                    }).ToArray();
                    var sine = prices.Select((_, i) => Math.Sin(i * Math.Sign(correlations.Take(i + 1).Sum()) / (double)period)).ToArray();
                    // This channel deliberately divides its cumulative sums by bar index, not count.
                    var priceMeans = prices.Select((_, i) => i == 0 ? 0 : prices.Take(i + 1).Sum() / i).ToArray();
                    var sineOffsets = sine.Select((v, i) => i == 0 ? 0 : v - sine.Take(i + 1).Sum() / i).ToArray();
                    var priceOffsets = prices.Select((v, i) => Math.Abs(v - priceMeans[i])).ToArray();
                    var line = prices.Select((_, i) =>
                    {
                        if (i == 0) return 0;
                        var sineDeviation = sineOffsets.Take(i + 1).Sum(Math.Abs) / i;
                        var sineZ = sineDeviation == 0 ? 0 : sineOffsets[i] / sineDeviation;
                        return priceMeans[i] + ((i > 1 ? 2 : 0) + sineZ) * priceOffsets.Take(i + 1).Sum() / i;
                    }).ToArray();
                    var errors = prices.Select((v, i) => Math.Abs(v - line[i])).ToArray();
                    var width = errors.Select((_, i) => i == 0 ? 0 : errors.Take(i + 1).Sum() / i).ToArray();
                    double[] Band(int multiple) => line.Select((v, i) => v + multiple * width[i]).ToArray();
                    return Outputs(("K", line), ("Os", width), ("Ap", Band(1)), ("Bp", Band(2)), ("Cp", Band(3)),
                        ("Al", Band(-1)), ("Bl", Band(-2)), ("Cl", Band(-3)));
                });
            case IndicatorName.VortexBands:
                return new("UpperBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
                {
                    double[] Smooth(double[] values)
                    {
                        if (!mcNicholl) return Average(values, length, kind);
                        var period = Math.Max(2, length);
                        var first = Average(values, period, 3);
                        var second = Average(first, period, 3);
                        var correction = (period + 1d) / (period - 1d);
                        return first.Select((v, i) => v + correction * (v - second[i])).ToArray();
                    }
                    var basis = Smooth(Closes(bars));
                    var widths = Smooth(bars.Select((b, i) => Math.Abs(b.Close - basis[i])).ToArray());
                    return Outputs(("MiddleBand", basis), ("UpperBand", basis.Select((v, i) => v + 2 * Math.Max(0, widths[i])).ToArray()),
                        ("LowerBand", basis.Select((v, i) => v - 2 * Math.Max(0, widths[i])).ToArray()));
                });
            case IndicatorName.HirashimaSugitaRS:
                return new("MiddleBand", new[] { "UpperBand1", "UpperBand2", "MiddleBand", "LowerBand1", "LowerBand2" }, bars =>
                {
                    var mean = Average(Closes(bars), length, 3);
                    var residual = bars.Select((b, i) => b.Close - mean[i]).ToArray();
                    var firstFit = RegressionEndpoints(residual, length);
                    var secondFit = RegressionEndpoints(residual.Zip(firstFit, (r, f) => r - f).ToArray(), length);
                    var center = mean.Select((v, i) => v + firstFit[i] + secondFit[i] - (i == 0 ? 0 : secondFit[i - 1])).ToArray();
                    var width = Average(residual.Select(Math.Abs).ToArray(), length, kind);
                    double[] Band(int multiplier) => center.Zip(width, (c, w) => c + multiplier * w).ToArray();
                    return Outputs(("MiddleBand", center), ("UpperBand1", Band(1)), ("UpperBand2", Band(2)),
                        ("LowerBand1", Band(-1)), ("LowerBand2", Band(-2)));
                });
            case IndicatorName.HurstBands:
                return new("MiddleBand", new[] { "UpperExtremeBand", "UpperOuterBand", "UpperInnerBand", "MiddleBand", "LowerExtremeBand", "LowerOuterBand", "LowerInnerBand" }, bars =>
                {
                    var lag = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d) + 1));
                    var delayed = bars.Select((_, i) => i < lag ? 0 : bars[i - lag].Close).ToArray();
                    var basis = delayed.Select((_, i) => Window(delayed, i, length).Average()).ToArray();
                    var result = new Dictionary<string, double[]> { ["MiddleBand"] = basis };
                    foreach (var (label, factor) in new[] { ("Extreme", Number(options, 4.2, "ExtremeMult")),
                        ("Outer", Number(options, 2.6, "OuterMult")), ("Inner", Number(options, 1.6, "InnerMult")) })
                    {
                        result["Upper" + label + "Band"] = basis.Select(v => v + v * factor / 100).ToArray();
                        result["Lower" + label + "Band"] = basis.Select(v => v - v * factor / 100).ToArray();
                    }
                    return result;
                });
            case IndicatorName.HurstCycleChannel:
                return new("FastMiddleBand", new[] { "FastUpperBand", "SlowUpperBand", "FastMiddleBand", "SlowMiddleBand", "FastLowerBand", "SlowLowerBand", "OMed", "OShort" }, bars =>
                {
                    int Half(int value) => Math.Max(2, Math.Min(530, (int)Math.Ceiling(value / 2d)));
                    var fast = Half(Integer(options, "FastLength", 10));
                    var slow = Half(Integer(options, "SlowLength", 30));
                    var fastAverage = Average(Closes(bars), fast, kind);
                    var slowAverage = Average(Closes(bars), slow, kind);
                    var fastWidth = Average(TrueRanges(bars), fast, kind).Select(v => v * Number(options, 1, "FastMult")).ToArray();
                    var slowWidth = Average(TrueRanges(bars), slow, kind).Select(v => v * Number(options, 3, "SlowMult")).ToArray();
                    var fastCenter = bars.Select((b, i) => i < Half(fast) ? b.Close : fastAverage[i - Half(fast)]).ToArray();
                    var slowCenter = bars.Select((b, i) => i < Half(slow) ? b.Close : slowAverage[i - Half(slow)]).ToArray();
                    double[] Band(double[] center, double[] width, int sign) => center.Zip(width, (c, w) => c + sign * w).ToArray();
                    return Outputs(("FastMiddleBand", fastCenter), ("SlowMiddleBand", slowCenter),
                        ("FastUpperBand", Band(fastCenter, fastWidth, 1)), ("FastLowerBand", Band(fastCenter, fastWidth, -1)),
                        ("SlowUpperBand", Band(slowCenter, slowWidth, 1)), ("SlowLowerBand", Band(slowCenter, slowWidth, -1)),
                        ("OMed", fastCenter.Select((v, i) => slowWidth[i] == 0 ? 0 : .5 + (v - slowCenter[i]) / (2 * slowWidth[i])).ToArray()),
                        ("OShort", bars.Select((b, i) => slowWidth[i] == 0 ? 0 : .5 + (b.Close - slowCenter[i]) / (2 * slowWidth[i])).ToArray()));
                });
            case IndicatorName.ValueChartIndicator:
                return new("vClose", new[] { "vClose", "vOpen", "vHigh", "vLow" }, bars =>
                {
                    var period = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 5d)));
                    var basis = Average(bars.Select(b => (b.High + b.Low) / 2).ToArray(), length, kind);
                    var ranges = bars.Select((_, i) => Window(bars, i, period).Max(b => b.High) - Window(bars, i, period).Min(b => b.Low)).ToArray();
                    var scale = ranges.Select((_, i) => Window(ranges, i, 5).Sum() / 25).ToArray();
                    double[] Coordinate(Func<Bar, double> price) => bars.Select((b, i) => scale[i] == 0 ? 0 : (price(b) - basis[i]) / scale[i]).ToArray();
                    return Outputs(("vClose", Coordinate(b => b.Close)), ("vOpen", Coordinate(b => b.Open)),
                        ("vHigh", Coordinate(b => b.High)), ("vLow", Coordinate(b => b.Low)));
                });
            case IndicatorName.WilsonRelativePriceChannel:
                return new("S1", new[] { "S1", "S2", "U1", "U2" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var gains = Average(changes.Select(v => Math.Max(0, v)).ToArray(), length, kind);
                    var losses = Average(changes.Select(v => Math.Max(0, -v)).ToArray(), length, kind);
                    var rsi = gains.Select((v, i) => losses[i] == 0 ? 100 : 100 * v / (v + losses[i])).ToArray();
                    double[] Channel(double threshold)
                    {
                        var distance = Average(rsi.Select(v => v - threshold).ToArray(), Integer(options, "SmoothLength", 1), kind);
                        return bars.Select((b, i) => b.Close * (1 - distance[i] / 100)).ToArray();
                    }
                    return Outputs(("S1", Channel(Number(options, 30, "Oversold"))), ("S2", Channel(Number(options, 45, "LowerNeutralZone"))),
                        ("U1", Channel(Number(options, 70, "Overbought"))), ("U2", Channel(Number(options, 55, "UpperNeutralZone"))));
                });
            case IndicatorName.TimeAndMoneyChannel:
                return new("Median", new[] { "Ch+1", "Ch-1", "Ch+2", "Ch-2", "Ch+3", "Ch-3", "Median" }, bars =>
                {
                    var basisPeriod = Integer(options, "Length1", 41);
                    var variancePeriod = Integer(options, "Length2", 82);
                    var lag = Math.Max(2, Math.Min(530, (int)Math.Ceiling(basisPeriod / 2d)));
                    var basis = Average(Closes(bars), basisPeriod, kind);
                    var returns = bars.Select((b, i) => i < lag || basis[i - lag] == 0 ? 0 : 100 * (b.Close - basis[i - lag]) / basis[i - lag]).ToArray();
                    var mean = Average(returns, variancePeriod, kind);
                    var square = Average(returns.Select(v => v * v).ToArray(), variancePeriod, kind);
                    var variance = kind == 1 ? PopulationVariance(returns, variancePeriod)
                        : square.Select((v, i) => Math.Max(0, v - mean[i] * mean[i])).ToArray();
                    var laggedDeviation = bars.Select((_, i) => i < lag ? 0 : Math.Sqrt(variance[i - lag])).ToArray();
                    var width = Average(laggedDeviation, basisPeriod, kind);
                    var result = new Dictionary<string, double[]> { ["Median"] = width };
                    foreach (var multiplier in new[] { 1, -1, 2, -2, 3, -3 })
                        result["Ch" + (multiplier > 0 ? "+" : "-") + Math.Abs(multiplier)] = basis.Select((v, i) => v * (1 + multiplier * .01 * width[i])).ToArray();
                    return result;
                });
            default: return null;
        }
    }
}
