using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? DiscoveredOutputs(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var kind = AverageKind(options, 3);
        switch (indicator.BatchName)
        {
            case IndicatorName.DecisionPointBreadthSwenlinTradingOscillator:
                var breadthLength = Integer(options, "Length", 5);
                return new("Dpbsto", new[] { "Dpbsto", "Signal" }, bars =>
                {
                    // Single-issue breadth: +1000/-1000 for an advance/decline, zero when unchanged.
                    var votes = bars.Select((bar, i) => 1000d * Math.Sign(bar.Close - (i == 0 ? 0 : bars[i - 1].Close))).ToArray();
                    var line = Average(Average(votes, breadthLength, 3), breadthLength, 3);
                    return Outputs(("Dpbsto", line), ("Signal", Average(line, breadthLength, 3)));
                });
            case IndicatorName.DiNapoliPreferredStochasticOscillator:
                var dinapoliLength = Integer(options, "Length", 8);
                return new("Dpso", new[] { "Dpso", "Signal" }, bars =>
                {
                    var fast = bars.Select((bar, i) =>
                    {
                        var window = Window(bars, i, dinapoliLength).ToArray();
                        var lower = window.Min(b => b.Low);
                        var range = window.Max(b => b.High) - lower;
                        return range == 0 ? 0 : 100 * (bar.Close - lower) / range;
                    }).ToArray();
                    var line = Average(fast, 3, 6);
                    return Outputs(("Dpso", line), ("Signal", Average(line, 3, 6)));
                });
            case IndicatorName.DTOscillator:
                kind = AverageKind(options, 6);
                if (kind == 0) return null;
                var dtLength = Integer(options, "Length", 13);
                return new("Dto", new[] { "Dto", "Signal" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var gains = Average(changes.Select(change => Math.Max(0, change)).ToArray(), dtLength, kind);
                    var losses = Average(changes.Select(change => Math.Max(0, -change)).ToArray(), dtLength, kind);
                    var rsi = gains.Select((gain, i) => losses[i] == 0 ? 100 : 100 * gain / (gain + losses[i])).ToArray();
                    if (dtLength > 1 && (kind == 3 || kind == 6))
                        for (var i = 1; i < rsi.Length; i++)
                            if (changes[i] == 0) rsi[i] = rsi[i - 1];
                    var stochastic = rsi.Select((value, i) =>
                    {
                        var window = Window(rsi, i, 8).ToArray();
                        var lower = window.Min();
                        var range = window.Max() - lower;
                        return range == 0 ? 0 : 100 * (value - lower) / range;
                    }).ToArray();
                    // DT uses arithmetic smoothing with available observations during startup.
                    var line = stochastic.Select((_, i) => Window(stochastic, i, 5).Average()).ToArray();
                    return Outputs(("Dto", line), ("Signal", line.Select((_, i) => Window(line, i, 3).Average()).ToArray()));
                });
            case IndicatorName.ConnorsRelativeStrengthIndex:
            case IndicatorName.StochasticConnorsRelativeStrengthIndex:
            case IndicatorName.QuasiWhiteNoise:
                kind = AverageKind(options, 6);
                if (kind == 0) return null;
                var noise = indicator.BatchName == IndicatorName.QuasiWhiteNoise;
                var stochastic = indicator.BatchName == IndicatorName.StochasticConnorsRelativeStrengthIndex;
                var streakLength = noise ? Integer(options, "NoiseLength", 500) : Integer(options, "Length1", 2);
                var priceLength = noise ? streakLength : Integer(options, "Length2", Integer(options, "Length", 3));
                var rankLength = noise ? Integer(options, "Length", 20) : Integer(options, "Length3", 100);
                var connorsKeys = noise ? new[] { "WhiteNoise", "WhiteNoiseMa", "WhiteNoiseStdDev", "WhiteNoiseVariance" }
                    : stochastic ? new[] { "SaRsi", "Signal" } : new[] { "Rsi", "PctRank", "StreakRsi", "ConnorsRsi" };
                return new(noise ? "WhiteNoise" : stochastic ? "SaRsi" : "ConnorsRsi", connorsKeys, bars =>
                {
                    var parts = ConnorsTrajectories(Closes(bars), streakLength, priceLength, rankLength, kind);
                    if (!noise && !stochastic) return parts;
                    var line = parts["ConnorsRsi"];
                    if (noise)
                    {
                        var divisor = Number(options, 40, "Divisor");
                        var values = line.Select(value => (value - 50) / divisor).ToArray();
                        var variance = PopulationVariance(values, streakLength);
                        return Outputs(("WhiteNoise", values), ("WhiteNoiseMa", Average(values, streakLength, kind)),
                            ("WhiteNoiseStdDev", variance.Select(Math.Sqrt).ToArray()), ("WhiteNoiseVariance", variance));
                    }
                    var fast = line.Select((value, i) =>
                    {
                        var window = Window(line, i, priceLength).ToArray();
                        var range = window.Max() - window.Min();
                        return range == 0 ? 0 : 100 * (value - window.Min()) / range;
                    }).ToArray();
                    var slow = Average(fast, Integer(options, "SmoothLength1", 3), kind);
                    return Outputs(("SaRsi", slow), ("Signal", Average(slow, Integer(options, "SmoothLength2", 3), kind)));
                });
            case IndicatorName.TurboScaler:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                var turboPeriod = Integer(options, "Length", 50);
                return new("Ts", new[] { "Ts", "Trigger" }, bars =>
                {
                    var prices = Closes(bars);
                    var mean = Average(prices, turboPeriod, kind);
                    var second = Average(mean, turboPeriod, kind);
                    double[] Position(double[] input, double[] center)
                    {
                        var blend = input.Select((v, i) => (v + center[i]) / 2).ToArray();
                        return input.Select((v, i) =>
                        {
                            var window = Window(blend, i, turboPeriod).ToArray();
                            var low = window.Min();
                            var range = window.Max() - low;
                            return range == 0 ? 0 : (v - low) / range;
                        }).ToArray();
                    }
                    return Outputs(("Ts", Position(prices, mean)), ("Trigger", Position(mean, second)));
                });
            case IndicatorName.MoveTracker:
                return new("Mt", new[] { "Mt", "Signal" }, bars =>
                {
                    var velocity = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var acceleration = bars.Select((b, i) => i == 0 ? 0 : i == 1 ? velocity[i]
                        : b.Close - 2 * bars[i - 1].Close + bars[i - 2].Close).ToArray();
                    return Outputs(("Mt", velocity), ("Signal", acceleration));
                });
            case IndicatorName.KurtosisIndicator:
                return new("Fk", new[] { "Fk", "Signal" }, bars =>
                {
                    var momentum = bars.Select((b, i) => i < 3 ? 0 : b.Close - bars[i - 3].Close).ToArray();
                    var changes = momentum.Select((v, i) => i == 0 ? 0 : v - momentum[i - 1]).ToArray();
                    var line = Average(changes, 65, 3);
                    return Outputs(("Fk", line), ("Signal", Average(line, 3, 2)));
                });
            case IndicatorName.DoubleSmoothedRelativeStrengthIndex:
                return new("Dsrsi", new[] { "Dsrsi", "Signal" }, bars =>
                {
                    var prices = Closes(bars);
                    var up = prices.Select((v, i) => v - Window(prices, i, 2).Min()).ToArray();
                    var down = prices.Select((v, i) => Window(prices, i, 2).Max() - v).ToArray();
                    var numerator = Average(Average(up, 5, 3), 25, 3);
                    var opposite = Average(Average(down, 5, 3), 25, 3);
                    var line = numerator.Select((v, i) => opposite[i] == 0 ? 100 : 100 * v / (v + opposite[i])).ToArray();
                    return Outputs(("Dsrsi", line), ("Signal", Average(line, 25, 3)));
                });
            case IndicatorName.DemandOscillator:
                if (kind == 0) return null;
                return new("Do", new[] { "Do", "Signal" }, bars =>
                {
                    var ranges = bars.Select((_, i) =>
                    {
                        var window = Window(bars, i, 2).ToArray();
                        return window.Max(b => b.High) - window.Min(b => b.Low);
                    }).ToArray();
                    var typicalRange = Average(ranges, 10, kind);
                    var imbalance = bars.Select((b, i) =>
                    {
                        var previous = i == 0 ? 0 : bars[i - 1].Close;
                        var change = b.Close - previous;
                        var reciprocal = previous == 0 || change == 0 || b.Close == 0 || typicalRange[i] == 0 ? 0
                            : b.Volume * typicalRange[i] * Math.Abs(previous) / (300 * b.Close * change);
                        return (b.Close > previous ? 1 : -1) * (b.Volume - reciprocal);
                    }).ToArray();
                    var line = Average(imbalance, 20, kind);
                    return Outputs(("Do", line), ("Signal", Average(line, 10, kind)));
                });
            case IndicatorName.McClellanOscillator:
                if (kind == 0) return null;
                return new("Mo", new[] { "AdvSum", "DecSum", "Mo", "Signal", "Histogram" }, bars =>
                {
                    var directions = bars.Select((b, i) => Math.Sign(b.Close - (i == 0 ? 0 : bars[i - 1].Close))).ToArray();
                    var advances = directions.Select((_, i) => (double)Window(directions, i, 19).Count(d => d > 0)).ToArray();
                    var declines = directions.Select((_, i) => (double)Window(directions, i, 19).Count(d => d < 0)).ToArray();
                    var net = advances.Select((v, i) => v + declines[i] == 0 ? 0 : 1000 * (v - declines[i]) / (v + declines[i])).ToArray();
                    var fast = Average(net, 19, kind);
                    var slow = Average(net, 39, kind);
                    var line = fast.Select((v, i) => v - slow[i]).ToArray();
                    var signal = Average(line, 9, kind);
                    return Outputs(("AdvSum", advances), ("DecSum", declines), ("Mo", line), ("Signal", signal),
                        ("Histogram", line.Select((v, i) => v - signal[i]).ToArray()));
                });
            case IndicatorName.DynamicMomentumIndex:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Dmi", new[] { "Dmi", "Signal", "Histogram" }, bars =>
                {
                    // Chande/Kroll relative-volatility period, also documented by Trady's DMI:
                    // https://github.com/gpmnet/Trady/blob/c5885341359af17e04ad5b7854a11e3678d3a89f/Trady.Analysis/Indicator/DynamicMomentumIndex.cs
                    var deviation = PopulationVariance(Closes(bars), 5).Select(Math.Sqrt).ToArray();
                    var average = Average(deviation, 10, kind);
                    var periods = deviation.Select((v, i) =>
                    {
                        if (average[i] <= 0) return 14;
                        if (v <= 0) return 30;
                        var raw = 14 / (v / average[i]);
                        // The numerical contract includes a relative 1e-12 tolerance at integer boundaries.
                        return (int)Math.Max(5, Math.Min(30, Math.Floor(raw + 1e-12 * Math.Max(1, raw))));
                    }).ToArray();
                    var gains = bars.Select((b, i) => i == 0 ? 0 : Math.Max(0, b.Close - bars[i - 1].Close)).ToArray();
                    var losses = bars.Select((b, i) => i == 0 ? 0 : Math.Max(0, bars[i - 1].Close - b.Close)).ToArray();
                    var line = bars.Select((_, i) =>
                    {
                        var up = Window(gains, i, periods[i]).Sum();
                        var down = Window(losses, i, periods[i]).Sum();
                        return down == 0 ? 100 : 100 * up / (up + down);
                    }).ToArray();
                    var signal = line.Select((_, i) => Window(line, i, periods[i]).Average()).ToArray();
                    return Oscillator("Dmi", line, signal);
                });
            default: return null;
        }
    }
    private static IReadOnlyDictionary<string, double[]> ConnorsTrajectories(double[] prices,
        int streakLength, int priceLength, int rankLength, int kind)
    {
        // Connors' rank is strict, over the preceding N one-bar returns. Ties are not advances.
        // Independently confirmed against Stock.Indicators ConnorsRsi.Series.cs (CalcStreak).
        var changes = prices.Select((value, i) => i == 0 ? 0 : value - prices[i - 1]).ToArray();
        var direction = changes.Select(Math.Sign).ToArray();
        var streak = prices.Select((_, i) => direction[i] == 0 ? 0d : direction[i]
            * (double)direction.Take(i + 1).Reverse().TakeWhile(value => value == direction[i]).Count()).ToArray();
        double[] Strength(double[] values, int period)
        {
            var differences = values.Select((value, i) => i == 0 ? 0 : value - values[i - 1]).ToArray();
            var up = Average(differences.Select(value => Math.Max(0, value)).ToArray(), period, kind);
            var down = Average(differences.Select(value => Math.Max(0, -value)).ToArray(), period, kind);
            var strength = up.Select((value, i) => down[i] == 0 ? 100 : 100 * value / (value + down[i])).ToArray();
            // Equal gain/loss decay cancels exactly on unchanged samples.
            if (period > 1 && (kind == 3 || kind == 6))
                for (var i = 1; i < strength.Length; i++)
                    if (differences[i] == 0) strength[i] = strength[i - 1];
            return strength;
        }
        var returns = changes.Select((change, i) => i == 0 || prices[i - 1] == 0 ? 0 : change / prices[i - 1] * 100).ToArray();
        var rank = returns.Select((value, i) => 100d / rankLength
            * returns.Skip(Math.Max(0, i - rankLength)).Take(Math.Min(i, rankLength)).Count(previous => previous < value)).ToArray();
        var priceRsi = Strength(prices, priceLength);
        var streakRsi = Strength(streak, streakLength);
        var composite = rank.Select((value, i) => (priceRsi[i] + streakRsi[i] + value) / 3).ToArray();
        return Outputs(("Rsi", priceRsi), ("PctRank", rank), ("StreakRsi", streakRsi), ("ConnorsRsi", composite));
    }

}
