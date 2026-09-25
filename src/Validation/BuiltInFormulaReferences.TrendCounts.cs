using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? TrendCounts(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 14);
        switch (indicator.BatchName)
        {
            case IndicatorName.ParabolicSAR:
                return new("Sar", new[] { "Sar" }, bars =>
                {
                    var result = new double[bars.Count];
                    var initial = (decimal)Number(options, .02, "Start");
                    var increment = (decimal)Number(options, .02, "Increment");
                    var maximum = (decimal)Number(options, .2, "Maximum");
                    var rising = true; var segment = 0;
                    decimal stop = 0;
                    for (var i = 0; i < bars.Count; i++)
                    {
                        if (i == 0) { stop = (decimal)bars[i].Low; result[i] = (double)stop; continue; }
                        // Reconstruct the extreme and count record-setting bars in the current trend.
                        var extreme = (decimal)(rising ? bars[segment].High : bars[segment].Low);
                        var records = 0;
                        for (var j = segment + 1; j < i; j++)
                        {
                            var candidate = (decimal)(rising ? bars[j].High : bars[j].Low);
                            if (rising ? candidate > extreme : candidate < extreme) { extreme = candidate; records++; }
                        }
                        stop += Math.Min(maximum, initial + records * increment) * (extreme - stop);
                        var prior = bars.Skip(Math.Max(0, i - 2)).Take(Math.Min(2, i));
                        stop = rising ? Math.Min(stop, (decimal)prior.Min(b => b.Low)) : Math.Max(stop, (decimal)prior.Max(b => b.High));
                        if (rising ? (decimal)bars[i].Low < stop : (decimal)bars[i].High > stop)
                        {
                            stop = rising ? Math.Max(extreme, (decimal)bars[i].High) : Math.Min(extreme, (decimal)bars[i].Low);
                            rising = !rising; segment = i;
                        }
                        result[i] = (double)stop;
                    }
                    return Outputs(("Sar", result));
                });
            case IndicatorName.TimePriceIndicator:
                return new("UpperBand", new[] { "UpperBand", "LowerBand" }, bars =>
                {
                    var highBreaks = bars.Select((b, i) => b.High > (i == 0 ? 0 : Window(bars, i - 1, length).Max(v => v.High))).ToArray();
                    var lowBreaks = bars.Select((b, i) => b.Low < (i == 0 ? 0 : Window(bars, i - 1, length).Min(v => v.Low))).ToArray();
                    double[] Elapsed(bool[] events) => events.Select((_, i) =>
                        Math.Min(length, i - Enumerable.Range(0, i + 1).Where(j => events[j]).DefaultIfEmpty(-1).Last()) / (double)length - .5).ToArray();
                    return Outputs(("UpperBand", Elapsed(highBreaks)), ("LowerBand", Elapsed(lowBreaks)));
                });
            case IndicatorName.ZweigMarketBreadthIndicator:
                var breadthKind = AverageKind(options, 3);
                if (breadthKind == 0) return null;
                return new("Zmbti", new[] { "Zmbti" }, bars =>
                {
                    var direction = bars.Select((b, i) => Math.Sign(b.Close - (i == 0 ? 0 : bars[i - 1].Close))).ToArray();
                    var proportion = direction.Select((_, i) =>
                    {
                        var changes = Window(direction, i, length).Where(d => d != 0).ToArray();
                        return changes.Length == 0 ? 0 : changes.Count(d => d > 0) / (double)changes.Length;
                    }).ToArray();
                    return Outputs(("Zmbti", Average(proportion, length, breadthKind)));
                });
            case IndicatorName.NaturalMarketSlope:
                return new("Nms", new[] { "Nms" }, bars =>
                {
                    var fitted = RegressionEndpoints(bars.Select(b => b.Close > 0 ? 1000 * Math.Log(b.Close) : 0).ToArray(), length);
                    return Outputs(("Nms", fitted.Select((v, i) => (v - (i == 0 ? 0 : fitted[i - 1])) * Math.Log(length)).ToArray()));
                });
            case IndicatorName.TrendStep:
                return new("Ts", new[] { "Ts" }, bars =>
                {
                    var prices = Closes(bars);
                    var variance = PopulationVariance(prices, length);
                    var line = new double[bars.Count];
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var previous = i == 0 ? prices[i] : line[i - 1];
                        line[i] = i < length || Math.Abs(prices[i] - previous) > 2 * Math.Sqrt(variance[i]) ? prices[i] : previous;
                    }
                    return Outputs(("Ts", line));
                });
            case IndicatorName.MarketDirectionIndicator:
                var fastPeriod = Integer(options, "FastLength", length);
                var slowPeriod = Integer(options, "SlowLength", 55);
                return new("Mdi", new[] { "Mdi" }, bars =>
                {
                    var prices = Closes(bars);
                    var crossings = prices.Select((_, i) => slowPeriod == fastPeriod ? 0 :
                        (fastPeriod * Window(prices, i, slowPeriod - 1).Sum()
                            - slowPeriod * Window(prices, i, fastPeriod - 1).Sum()) / (slowPeriod - fastPeriod)).ToArray();
                    return Outputs(("Mdi", prices.Select((v, i) =>
                    {
                        var previous = i == 0 ? 0 : prices[i - 1];
                        return v + previous == 0 ? 0 : 200 * ((i == 0 ? 0 : crossings[i - 1]) - crossings[i]) / (v + previous);
                    }).ToArray()));
                });
            case IndicatorName.JapaneseCorrelationCoefficient:
                var japaneseKind = AverageKind(options, 1);
                if (japaneseKind == 0) return null;
                return new("Jo", new[] { "Jo" }, bars =>
                {
                    var half = Clamp((int)Math.Ceiling(length / 2d), 2, 530);
                    var highs = Average(bars.Select(b => b.High).ToArray(), half, japaneseKind);
                    var lows = Average(bars.Select(b => b.Low).ToArray(), half, japaneseKind);
                    var closes = Average(Closes(bars), half, japaneseKind);
                    return Outputs(("Jo", closes.Select((v, i) =>
                    {
                        var high = ReferenceFraction.FromDouble(Window(highs, i, half).Max());
                        var low = ReferenceFraction.FromDouble(Window(lows, i, half).Min());
                        var range = high - low;
                        var change = ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(i < length ? 0 : closes[i - length]);
                        return range.CompareTo(new ReferenceFraction(0)) == 0 ? 0 : (change / range).ToDouble();
                    }).ToArray()));
                });
            case IndicatorName.FastSlowDegreeOscillator:
                var degreeKind = AverageKind(options, 3);
                if (degreeKind == 0) return null;
                return new("Fsdo", new[] { "Fsdo", "Signal", "Histogram" }, bars =>
                {
                    var fastDegree = Integer(options, "FastLength", 3);
                    var slowDegree = Integer(options, "SlowLength", 2);
                    // The shared quadratic terms cancel; only different sine-window tails remain.
                    var differences = bars.Select((_, i) => (Math.Sin(Math.PI * (i + 1d) * (i + 1d) / length)
                        - Math.Sin(Math.PI * i * (i + 1d) / length)) / (i + 1d)).ToArray();
                    var weighted = bars.Select((_, i) => i == 0 ? 0 : bars[i - 1].Close
                        * (Window(differences, i, fastDegree).Sum() - Window(differences, i, slowDegree).Sum())).ToArray();
                    var line = weighted.Select((_, i) => Window(weighted, i, length).Sum()).ToArray();
                    var signal = Average(line, Integer(options, "SignalLength", 14), degreeKind);
                    return Outputs(("Fsdo", line), ("Signal", signal), ("Histogram", line.Zip(signal, (v, avg) => v - avg).ToArray()));
                });
            case IndicatorName.TrendDirectionForceIndex:
                var forceKind = AverageKind(options, 3);
                if (forceKind == 0) return null;
                return new("Tdfi", new[] { "Tdfi" }, bars =>
                {
                    var period = Clamp((Integer(options, "Length1", 10) + 1) / 2, 1, 530);
                    if (forceKind == 3) return Outputs(("Tdfi", ExactExponentialForce(Closes(bars), period, Integer(options, "Length2", 30))));
                    // The common price scale cancels in normalization (force is homogeneous of degree four).
                    var first = Average(Closes(bars), period, forceKind);
                    var second = Average(first, period, forceKind);
                    var midpoint = first.Zip(second, (f, s) => (f + s) / 2).ToArray();
                    var force = midpoint.Select((m, i) => Math.Abs(first[i] - second[i])
                        * Math.Pow(m - (i == 0 ? 0 : midpoint[i - 1]), 3)).ToArray();
                    var lookback = Integer(options, "Length2", 30);
                    var line = force.Select((f, i) =>
                    {
                        var scale = Window(force, i, lookback).Max(Math.Abs);
                        return scale == 0 ? 0 : f / scale;
                    }).ToArray();
                    return Outputs(("Tdfi", line));
                });
            case IndicatorName.VolatilitySwitchIndicator:
                var switchKind = AverageKind(options, 2);
                if (switchKind == 0) return null;
                return new("Vsi", new[] { "Vsi" }, bars =>
                {
                    var returns = bars.Select((b, i) => i == 0 || b.Close + bars[i - 1].Close == 0 ? 0
                        : 2 * (b.Close - bars[i - 1].Close) / (b.Close + bars[i - 1].Close)).ToArray();
                    var deviations = PopulationVariance(returns, length).Select(Math.Sqrt).ToArray();
                    return Outputs(("Vsi", Average(deviations, length, switchKind)));
                });
            case IndicatorName.TotalPowerIndicator:
                var powerKind = AverageKind(options, 3);
                if (powerKind == 0) return null;
                return new("TotalPower", new[] { "TotalPower", "BullCount", "BearCount" }, bars =>
                {
                    var period = Integer(options, "Length1", 45);
                    var baseline = Average(Closes(bars), Integer(options, "Length2", 10), powerKind);
                    var bulls = bars.Select((b, i) => b.High > baseline[i]).ToArray();
                    var bears = bars.Select((b, i) => b.Low < baseline[i]).ToArray();
                    var bullish = bulls.Select((_, i) => 100d * Window(bulls, i, period).Count(v => v) / period).ToArray();
                    var bearish = bears.Select((_, i) => 100d * Window(bears, i, period).Count(v => v) / period).ToArray();
                    return Outputs(("BullCount", bullish), ("BearCount", bearish),
                        ("TotalPower", bullish.Zip(bearish, (b, s) => Math.Abs(b - s)).ToArray()));
                });
            case IndicatorName.TrendPersistenceRate:
                return new("Tpr", new[] { "Tpr" }, bars =>
                {
                    var baseline = Average(Closes(bars), 5, 3);
                    var votes = baseline.Select((_, i) =>
                    {
                        var slope = (i == 0 ? 0 : baseline[i - 1]) - (i < 2 ? 0 : baseline[i - 2]);
                        return Math.Abs(slope) > .01 ? Math.Sign(slope) : 0;
                    }).ToArray();
                    return Outputs(("Tpr", votes.Select((_, i) => 100d * Math.Abs(Window(votes, i, length).Sum()) / length).ToArray()));
                });
            case IndicatorName.TrendContinuationFactor:
                return new("TcfPlus", new[] { "TcfPlus", "TcfMinus" }, bars =>
                {
                    var prices = Closes(bars);
                    var changes = prices.Select((value, i) => i == 0 ? 0 : value - prices[i - 1]).ToArray();
                    var positive = new double[prices.Length];
                    var negative = new double[prices.Length];
                    for (var i = 1; i < prices.Length; i++)
                    {
                        var sign = Math.Sign(changes[i]);
                        var first = i;
                        while (first > 1 && Math.Sign(changes[first - 1]) == sign) first--;
                        // A continuation run telescopes to its endpoint displacement.
                        var run = Math.Abs(prices[i] - prices[first - 1]);
                        positive[i] = Math.Max(0, changes[i]) - (sign < 0 ? run : 0);
                        negative[i] = Math.Max(0, -changes[i]) - (sign > 0 ? run : 0);
                    }
                    return Outputs(("TcfPlus", positive.Select((_, i) => Window(positive, i, length).Sum()).ToArray()),
                        ("TcfMinus", negative.Select((_, i) => Window(negative, i, length).Sum()).ToArray()));
                });
            case IndicatorName.TrendExhaustionIndicator:
                var exhaustionKind = AverageKind(options, 1);
                if (exhaustionKind == 0) return null;
                return new("Tei", new[] { "Tei", "Signal" }, bars =>
                {
                    var advances = bars.Select((b, i) => b.Close > (i == 0 ? 0 : bars[i - 1].Close)).ToArray();
                    var breakouts = bars.Select((b, i) => b.High > (i == 0 ? 0
                        : Window(bars, i - 1, Math.Max(2, length)).Max(p => p.High))).ToArray();
                    var ratio = bars.Select((_, i) =>
                    {
                        var count = advances.Take(i + 1).Count(v => v);
                        return count == 0 ? 0 : (double)breakouts.Take(i + 1).Count(v => v) / count;
                    }).ToArray();
                    var gain = 2d / (length + 1);
                    var line = ratio.Select((_, i) => Enumerable.Range(0, i + 1)
                        .Sum(j => gain * Math.Pow(1 - gain, i - j) * ratio[j])).ToArray();
                    return Outputs(("Tei", line), ("Signal", Average(line, length, exhaustionKind)));
                });
            default: return null;
        }
    }

    private static double[] ExactExponentialForce(double[] prices, int period, int lookback)
    {
        ReferenceFraction[] AverageExactly(ReferenceFraction[] input)
        {
            var result = new ReferenceFraction[input.Length];
            var sum = new ReferenceFraction(0);
            var alpha = new ReferenceFraction(2) / new ReferenceFraction(period + 1);
            for (var i = 0; i < input.Length; i++)
            {
                if (i < period) { sum += input[i]; result[i] = sum / new ReferenceFraction(i + 1); }
                else result[i] = result[i - 1] + alpha * (input[i] - result[i - 1]);
            }
            return result;
        }
        var first = AverageExactly(prices.Select(ReferenceFraction.FromDouble).ToArray());
        var second = AverageExactly(first);
        var forces = new ReferenceFraction[prices.Length];
        var result = new double[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            var previous = i == 0 ? new ReferenceFraction(0) : first[i - 1] + second[i - 1];
            var change = (first[i] + second[i] - previous) / new ReferenceFraction(2);
            forces[i] = (first[i] - second[i]).Abs() * change * change * change;
            var maximum = new ReferenceFraction(0);
            for (var j = Math.Max(0, i - lookback + 1); j <= i; j++)
                if (forces[j].Abs().CompareTo(maximum) > 0) maximum = forces[j].Abs();
            result[i] = maximum.Sign == 0 ? 0 : (forces[i] / maximum).ToDouble();
        }
        return result;
    }
}
