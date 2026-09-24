using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static double[] MomentumVelocity(double[] prices, int period, double gain)
    {
        var momentum = prices.Select((v, i) => i < period ? 0 : v - prices[i - period]).ToArray();
        // H(z) = gain*(1-z^-1)/(1-(1-gain)*z^-1): its tail weights are -gain^2*(1-gain)^(lag-1).
        return momentum.Select((v, i) => gain * v - gain * gain * Enumerable.Range(0, i)
            .Sum(j => Math.Pow(1 - gain, i - j - 1) * momentum[j])).ToArray();
    }

    private static FormulaDefinition? MarketStructure(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 14);
        var kind = AverageKind(options, 3);
        switch (indicator.BatchName)
        {
            case IndicatorName.WilliamsFractals:
                var delay = Math.Max(2, length);
                return new("UpFractal", new[] { "UpFractal", "DnFractal" }, bars =>
                {
                    // The five published plateau shapes, as relations of older observations to the center.
                    // -1: strictly lower; 0: equal; 1: lower or equal. Reflect to identify troughs.
                    int[][] shapes = { new[] { -1, -1 }, new[] { 0, -1, -1 }, new[] { 1, 0, -1, -1 },
                        new[] { 1, 0, 0, -1, -1 }, new[] { 1, 0, 1, 0, -1, -1 } };
                    double[] Detect(bool upper)
                    {
                        var values = bars.Select(b => upper ? b.High : -b.Low).ToArray();
                        return bars.Select((_, i) =>
                        {
                            var center = i - delay;
                            if (center < 2 || values[center + 1] >= values[center] || values[center + 2] >= values[center]) return 0d;
                            return shapes.Any(shape => center >= shape.Length && shape.Select((relation, offset) =>
                            {
                                var older = values[center - offset - 1];
                                return relation == -1 ? older < values[center] : relation == 0 ? older == values[center] : older <= values[center];
                            }).All(matches => matches)) ? 1d : 0;
                        }).ToArray();
                    }
                    return Outputs(("UpFractal", Detect(true)), ("DnFractal", Detect(false)));
                });

            case IndicatorName.GuppyMultipleMovingAverage:
                if (kind == 0) return null;
                return new("SuperGmmaOsc", new[] { "SuperGmmaOsc", "SuperGmmaSignal" }, bars =>
                {
                    var fastPeriods = new[] { 1, 2, 3, 5, 7, 9, 10, 11, 12, 13, 14 }
                        .Select(j => Integer(options, "Length" + j, 1));
                    var slowPeriods = new[] { 15, 16, 18, 19, 21, 22, 23, 25, 26 }
                        .Select(j => Integer(options, "Length" + j, 1)).Concat(new[] { 52, 55, 58, 61, 64, 67, 70 });
                    var fast = fastPeriods.Select(period => Average(Closes(bars), period, kind)).ToArray();
                    var slow = slowPeriods.Select(period => Average(Closes(bars), period, kind)).ToArray();
                    var line = bars.Select((_, i) =>
                    {
                        var denominator = slow.Average(values => values[i]);
                        return denominator == 0 ? 0 : 100 * (fast.Average(values => values[i]) / denominator - 1);
                    }).ToArray();
                    // The V2 options expose no raw smoothing or signal period: legacy defaults are 1 and 13.
                    return Outputs(("SuperGmmaOsc", line), ("SuperGmmaSignal", Average(line, 13, kind)));
                });
            case IndicatorName.FastandSlowKurtosisOscillator:
                kind = AverageKind(options, 2);
                if (kind == 0) return null;
                var kurtosisRatio = Number(options, .03, "Ratio");
                return new("Fsk", new[] { "Fsk", "Signal" }, bars =>
                {
                    var line = MomentumVelocity(Closes(bars), length, kurtosisRatio);
                    return Outputs(("Fsk", line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.FastandSlowRelativeStrengthIndexOscillator:
            case IndicatorName.FastandSlowStochasticOscillator:
                var fastSlowRsi = indicator.BatchName == IndicatorName.FastandSlowRelativeStrengthIndexOscillator;
                var fastSlowKey = fastSlowRsi ? "Fsrsi" : "Fsst";
                return new(fastSlowKey, new[] { fastSlowKey, "Signal" }, bars =>
                {
                    // Both aliases have an obsolete Length option; the underlying periods are 3, 6 and 9.
                    var velocity = Average(MomentumVelocity(Closes(bars), 3, .03), 6, 2);
                    double[] level;
                    if (fastSlowRsi)
                    {
                        var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                        var gains = Average(changes.Select(v => Math.Max(v, 0)).ToArray(), 9, 2);
                        var losses = Average(changes.Select(v => Math.Max(-v, 0)).ToArray(), 9, 2);
                        level = gains.Select((v, i) => losses[i] == 0 ? 100 : 100 * v / (v + losses[i])).ToArray();
                    }
                    else
                    {
                        var raw = bars.Select((b, i) =>
                        {
                            var window = Window(bars, i, 9).ToArray();
                            var low = window.Min(v => v.Low);
                            var high = window.Max(v => v.High);
                            return high == low ? 0 : 100 * (b.Close - low) / (high - low);
                        }).ToArray();
                        level = Average(raw, 9, 2);
                    }
                    var line = level.Select((v, i) => v + (fastSlowRsi ? 10000 : 500) * velocity[i]).ToArray();
                    return Outputs((fastSlowKey, line), ("Signal", Average(line, fastSlowRsi ? 6 : 9, 2)));
                });
            case IndicatorName.GOscillator:
                return new("GOsc", new[] { "GOsc" }, bars =>
                {
                    var advances = bars.Select((b, i) => b.Close > (i == 0 ? 0 : bars[i - 1].Close)).ToArray();
                    return Outputs(("GOsc", bars.Select((_, i) => 100d / length * Window(advances, i, length).Count(v => v)).ToArray()));
                });
            case IndicatorName.GannSwingOscillator:
            case IndicatorName.GannTrendOscillator:
                var gannKey = indicator.BatchName == IndicatorName.GannSwingOscillator ? "Gso" : "Gto";
                return new(gannKey, new[] { gannKey }, bars =>
                {
                    var highs = bars.Select((_, i) => Window(bars, i, length).Max(b => b.High)).ToArray();
                    var lows = bars.Select((_, i) => Window(bars, i, length).Min(b => b.Low)).ToArray();
                    // Find the most recent extremum reversal; a bar with both triggers gives the high priority.
                    var events = bars.Select((_, i) =>
                    {
                        var highBefore = i < 2 ? 0 : highs[i - 2];
                        var highMiddle = i < 1 ? 0 : highs[i - 1];
                        var lowBefore = i < 2 ? 0 : lows[i - 2];
                        var lowMiddle = i < 1 ? 0 : lows[i - 1];
                        return highBefore > highMiddle && highs[i] > highMiddle ? 1d
                            : lowBefore < lowMiddle && lows[i] < lowMiddle ? -1d : 0d;
                    }).ToArray();
                    return Outputs((gannKey, events.Select((_, i) => events.Take(i + 1).LastOrDefault(v => v != 0)).ToArray()));
                });
            case IndicatorName.GannHiLoActivator:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Ghla", new[] { "Ghla" }, bars =>
                {
                    var highs = Average(bars.Select(b => b.High).ToArray(), length, kind);
                    var lows = Average(bars.Select(b => b.Low).ToArray(), length, kind);
                    var triggers = bars.Select((b, i) => b.Close > (i == 0 ? 0 : highs[i - 1]) ? 1
                        : b.Close < (i == 0 ? 0 : lows[i - 1]) ? -1 : 0).ToArray();
                    return Outputs(("Ghla", bars.Select((_, i) =>
                    {
                        var last = Enumerable.Range(0, i + 1).Where(j => triggers[j] != 0).DefaultIfEmpty(-1).Last();
                        return last < 0 ? 0 : triggers[last] > 0 ? lows[last] : highs[last];
                    }).ToArray()));
                });
            case IndicatorName.HighLowIndex:
                if (kind == 0) return null;
                return new("Zmbti", new[] { "Zmbti" }, bars =>
                {
                    var highs = bars.Select((_, i) => Window(bars, i, length).Max(b => b.High)).ToArray();
                    var lows = bars.Select((_, i) => Window(bars, i, length).Min(b => b.Low)).ToArray();
                    var advances = highs.Select((v, i) => v > (i == 0 ? 0 : highs[i - 1])).ToArray();
                    var declines = lows.Select((v, i) => v < (i == 0 ? 0 : lows[i - 1])).ToArray();
                    var proportion = bars.Select((_, i) =>
                    {
                        var up = Window(advances, i, length).Count(v => v);
                        var down = Window(declines, i, length).Count(v => v);
                        return up + down == 0 ? 0 : 100d * up / (up + down);
                    }).ToArray();
                    return Outputs(("Zmbti", Average(proportion, length, kind)));
                });
            case IndicatorName.GuppyDistanceIndicator:
                if (kind == 0) return null;
                return new("FastDistance", new[] { "FastDistance", "SlowDistance" }, bars =>
                {
                    double[] Distance(int first)
                    {
                        var ribbon = Enumerable.Range(first, 6).Select(j =>
                            Average(Closes(bars), Integer(options, "Length" + j, 1), kind)).ToArray();
                        return bars.Select((_, i) => Enumerable.Range(1, 5)
                            .Sum(j => Math.Abs(ribbon[j][i] - ribbon[j - 1][i]))).ToArray();
                    }
                    return Outputs(("FastDistance", Distance(1)), ("SlowDistance", Distance(7)));
                });
            case IndicatorName.FearAndGreedIndicator:
                kind = AverageKind(options, 2);
                if (kind == 0) return null;
                var fearFast = Integer(options, "FastLength", 10);
                var fearSlow = Integer(options, "SlowLength", 30);
                var fearSignal = Integer(options, "SmoothLength", 2);
                return new("Fgi", new[] { "Fgi", "Signal" }, bars =>
                {
                    // Linearity combines the separate up-range and down-range smoothing into signed range.
                    var ranges = TrueRanges(bars);
                    var directional = bars.Select((b, i) => i == 0 ? 0 :
                        Math.Sign(b.Close - bars[i - 1].Close) * ranges[i]).ToArray();
                    var fast = Average(directional, fearFast, kind);
                    var slow = Average(directional, fearSlow, kind);
                    var line = fast.Select((v, i) => v - slow[i]).ToArray();
                    return Outputs(("Fgi", line), ("Signal", Average(line, fearSignal, kind)));
                });
            default: return null;
        }
    }
}
