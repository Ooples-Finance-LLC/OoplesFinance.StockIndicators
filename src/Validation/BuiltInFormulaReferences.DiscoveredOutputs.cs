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
                if (AverageKind(options, 6) == 0) return null;
                return new("Dto", new[] { "Dto", "Signal" }, bars => DtOscillatorOutputs(bars, indicator));
            case IndicatorName.ConnorsRelativeStrengthIndex:
            case IndicatorName.StochasticConnorsRelativeStrengthIndex:
            case IndicatorName.QuasiWhiteNoise:
                kind = AverageKind(options, 6);
                if (kind == 0) return null;
                var noise = indicator.BatchName == IndicatorName.QuasiWhiteNoise;
                if (noise && kind is 1 or 2 or 3 or 6) return new("WhiteNoise", new[] { "WhiteNoise", "WhiteNoiseMa", "WhiteNoiseStdDev", "WhiteNoiseVariance" }, bars => QuasiWhiteNoiseOutputs(bars, indicator));
                if (!noise && kind is 1 or 2 or 3 or 6)
                    return new(indicator.BatchName == IndicatorName.ConnorsRelativeStrengthIndex ? "ConnorsRsi" : "SaRsi",
                        indicator.BatchName == IndicatorName.ConnorsRelativeStrengthIndex ? new[] { "Rsi", "PctRank", "StreakRsi", "ConnorsRsi" } : new[] { "SaRsi", "Signal" }, bars => ConnorsOutputs(bars, indicator));
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
                return new("Ts", new[] { "Ts", "Trigger" }, bars => TurboScalerOutputs(bars, indicator));
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
                return new("Dsrsi", new[] { "Dsrsi", "Signal" }, bars => RangeGainLossOutputs(bars, indicator));
            case IndicatorName.DemandOscillator:
                if (kind == 0) return null;
                return new("Do", new[] { "Do", "Signal" }, bars => DemandOscillatorOutputs(bars, indicator));
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
                return new("Dmi", new[] { "Dmi", "Signal", "Histogram" }, bars => DynamicMomentumOutputs(bars, indicator));
            default: return null;
        }
    }
    private static IReadOnlyDictionary<string, double[]> ConnorsTrajectories(double[] prices,
        int streakLength, int priceLength, int rankLength, int kind)
    {
        // Connors' rank is strict, over the preceding N one-bar returns. Ties are not advances.
        // Independently confirmed against Stock.Indicators ConnorsRsi.Series.cs (CalcStreak).
        var direction = prices.Select((value, i) => i == 0 ? 0 : value.CompareTo(prices[i - 1])).ToArray();
        var streak = prices.Select((_, i) => direction[i] == 0 ? 0d : direction[i]
            * (double)direction.Take(i + 1).Reverse().TakeWhile(value => value == direction[i]).Count()).ToArray();
        double[] Strength(double[] values, int period)
        {
            if (kind is 1 or 2 or 3 or 6)
                return RoundedPriceRsi(values.Select(v => new Bar(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), v, v, v, v, 1)).ToArray(), period, kind);
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
        var returns = prices.Select((value, i) => i == 0 || prices[i - 1] == 0 ? new ReferenceFraction(0) :
            (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(prices[i - 1])) / ReferenceFraction.FromDouble(prices[i - 1])).ToArray();
        var rank = returns.Select((value, i) => 100d
            * returns.Skip(Math.Max(0, i - rankLength)).Take(Math.Min(i, rankLength)).Count(previous => previous.CompareTo(value) < 0) / rankLength).ToArray();
        var priceRsi = Strength(prices, priceLength);
        var streakRsi = Strength(streak, streakLength);
        var composite = rank.Select((value, i) => ((ReferenceFraction.FromDouble(priceRsi[i]) + ReferenceFraction.FromDouble(streakRsi[i]) + ReferenceFraction.FromDouble(value)) / new ReferenceFraction(3)).ToDouble()).ToArray();
        return Outputs(("Rsi", priceRsi), ("PctRank", rank), ("StreakRsi", streakRsi), ("ConnorsRsi", composite));
    }

}
