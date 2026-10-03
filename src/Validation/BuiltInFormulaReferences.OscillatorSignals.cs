using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? OscillatorSignals(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 14);
        var kind = AverageKind(options, 3);
        if (indicator.BatchName == IndicatorName.FireflyOscillator &&
            options.GetType().GetProperty("MaType")?.GetValue(options) is MovingAvgType.ZeroLagExponentialMovingAverage) kind = 4;
        if (kind == 0) return null;
        switch (indicator.BatchName)
        {
            case IndicatorName.SentimentZoneOscillator:
                return new("Szo", new[] { "Szo" }, bars => Outputs(("Szo",
                    Average(bars.Select((b, i) => b.Close > (i == 0 ? 0 : bars[i - 1].Close) ? 1d : -1).ToArray(),
                        length, kind).Select(v => 100 * v / length).ToArray())));
            case IndicatorName.SpearmanIndicator:
                return new("Si", new[] { "Si", "Signal" }, bars =>
                {
                    // Historical library variant: correlate chronological midranks with their sorted
                    // midranks. Ties in both vectors distinguish this from correlation with time ranks.
                    var line = bars.Select((_, i) =>
                    {
                        var values = Window(bars, i, length).Select(b => b.Close).ToArray();
                        var ranks = values.Select(v => values.Count(x => x < v) + (values.Count(x => x == v) + 1) / 2d).ToArray(); // NOSONAR: S1244 - Rank ties require equal observations.
                        var mean = (values.Length + 1) / 2d;
                        var x = ranks.Select(v => v - mean).ToArray();
                        var y = x.OrderBy(v => v).ToArray();
                        var square = x.Sum(v => v * v);
                        return square == 0 ? 0 : 100 * x.Zip(y, (a, b) => a * b).Sum() / square;
                    }).ToArray();
                    return Outputs(("Si", line), ("Signal", Average(line, Integer(options, "SignalLength", 3), kind)));
                });
            case IndicatorName.TurboStochasticsFast:
            case IndicatorName.TurboStochasticsSlow:
                return new("Tsf", new[] { "Tsf", "Signal" }, bars => TurboStochasticsOutputs(bars, indicator));
            case IndicatorName.StochasticCustomOscillator:
                return new("Sco", new[] { "Sco", "Signal" }, bars =>
                {
                    var lows = bars.Select((_, i) => Window(bars, i, length).Min(b => b.Low)).ToArray();
                    var ranges = bars.Select((_, i) => Window(bars, i, length).Max(b => b.High) - lows[i]).ToArray();
                    var numerator = Average(bars.Select((b, i) => b.Close - lows[i]).ToArray(), 3, kind);
                    var denominator = Average(ranges, 3, kind);
                    var line = numerator.Select((v, i) => denominator[i] == 0 ? 0 : Math.Max(0, Math.Min(100, 100 * v / denominator[i]))).ToArray();
                    return Outputs(("Sco", line), ("Signal", Average(line, 12, kind)));
                });
            case IndicatorName.StochasticMovingAverageConvergenceDivergenceOscillator:
                return new("Macd", new[] { "Macd", "Signal", "Histogram" }, bars =>
                {
                    var fast = Average(Closes(bars), 12, 3);
                    var slow = Average(Closes(bars), 26, 3);
                    var line = bars.Select((_, i) =>
                    {
                        var window = Window(bars, i, length).ToArray();
                        var range = window.Max(b => b.High) - window.Min(b => b.Low);
                        return range == 0 ? 0 : 10 * (fast[i] - slow[i]) / range;
                    }).ToArray();
                    var signal = Average(line, 9, 3);
                    return Outputs(("Macd", line), ("Signal", signal), ("Histogram", line.Zip(signal, (v, m) => v - m).ToArray()));
                });
            case IndicatorName.UltimateTraderOscillator:
                return new("Uto", new[] { "Uto", "Signal" }, bars => UltimateTraderValues(bars,
                    (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!));
            case IndicatorName.TraderPressureIndex:
                if (kind is 1 or 2 or 3 or 6) return new("Tpx", new[] { "Tpx", "Bulls", "Bears" }, bars => ResidualPressureOutputs(bars, indicator));
                return new("Tpx", new[] { "Tpx", "Bulls", "Bears" }, bars =>
                {
                    var period = Integer(options, "Length1", 7);
                    var rangePeriod = Integer(options, "Length2", 2);
                    double[] Pressure(bool positive) => bars.Select((b, i) =>
                    {
                        var window = Window(bars, i, rangePeriod).ToArray();
                        var range = window.Max(v => v.High) - window.Min(v => v.Low);
                        var movements = new[] { b.High - (i == 0 ? 0 : bars[i - 1].High), b.Low - (i == 0 ? 0 : bars[i - 1].Low) };
                        var magnitude = movements.Where(v => positive ? v > 0 : v < 0).Sum(Math.Abs);
                        return range == 0 ? 0 : 100 * Math.Min(1, magnitude / range);
                    }).ToArray();
                    var bulls = Average(Pressure(true), period, kind);
                    var bears = Average(Pressure(false), period, kind);
                    return Outputs(("Tpx", Average(bulls.Zip(bears, (a, b) => a - b).ToArray(), Integer(options, "SmoothLength", 3), kind)),
                        ("Bulls", bulls), ("Bears", bears));
                });
            case IndicatorName.StrengthOfMovement:
                return new("Som", new[] { "Som" }, bars =>
                {
                    var period = Integer(options, "Length1", 10);
                    var lag = Integer(options, "Length2", 3) - 1;
                    var returns = bars.Select((b, i) => lag == 0 || i < lag || bars[i - lag].Close == 0 ? 0
                        : (b.Close - bars[i - lag].Close) / lag / bars[i - lag].Close).ToArray();
                    var average = Average(returns, period, kind);
                    var centered = average.Select((v, i) =>
                    {
                        var window = Window(average, i, Math.Max(2, period)).ToArray();
                        var range = window.Max() - window.Min();
                        return range == 0 ? -100 : 200 * (v - window.Min()) / range - 100;
                    }).ToArray();
                    return Outputs(("Som", Average(centered, 3, kind)));
                });
            case IndicatorName.TrendTriggerFactor:
                return new("Ttf", new[] { "Ttf" }, bars =>
                {
                    var highs = bars.Select((_, i) => Window(bars, i, length).Max(b => b.High)).ToArray();
                    var lows = bars.Select((_, i) => Window(bars, i, length).Min(b => b.Low)).ToArray();
                    var line = bars.Select((_, i) =>
                    {
                        var priorHigh = i < length ? 0 : highs[i - length];
                        var priorLow = i < length ? 0 : lows[i - length];
                        var totalRange = highs[i] - lows[i] + priorHigh - priorLow;
                        return totalRange == 0 ? 0 : 200 * ((highs[i] + lows[i]) - (priorHigh + priorLow)) / totalRange;
                    }).ToArray();
                    return Outputs(("Ttf", line));
                });
            case IndicatorName.RainbowOscillator:
                return new("Ro", new[] { "Ro", "UpperBand", "LowerBand" }, bars => RainbowOutputs(bars, indicator));
            case IndicatorName.FireflyOscillator:
                return new("Fo", new[] { "Fo", "Signal" }, bars =>
                {
                    var weighted = bars.Select(b => (b.High + b.Low + 2 * b.Close) / 4).ToArray();
                    var mean = Average(weighted, length, kind);
                    var variance = PopulationVariance(weighted, length);
                    var standardized = weighted.Select((v, i) => (v - mean[i]) * 100 / (variance[i] == 0 ? 1 : Math.Sqrt(variance[i]))).ToArray();
                    var filtered = Average(Average(Average(standardized, 3, kind), 3, kind), length, kind);
                    var line = filtered.Select(v => (v + 100) / 2 - 4).ToArray();
                    return Outputs(("Fo", line), ("Signal", line.Select((_, i) => Window(line, i, 3).Max()).ToArray()));
                });
            case IndicatorName.NormalizedMacd:
                return new("NormalizedMacd", new[] { "NormalizedMacd" }, bars =>
                {
                    var prices = Closes(bars);
                    var fast = ExpandedGainTrajectory(prices, Enumerable.Repeat(2d / (Integer(options, "FastLength", 12) + 1), prices.Length).ToArray());
                    var slow = ExpandedGainTrajectory(prices, Enumerable.Repeat(2d / (Integer(options, "SlowLength", 26) + 1), prices.Length).ToArray());
                    return Outputs(("NormalizedMacd", fast.Zip(slow, (f, s) => s == 0 ? 0 : 100 * (f / s - 1)).ToArray()));
                });
            case IndicatorName.SmoothedWilliamsR:
                return new("Swr", new[] { "Swr" }, bars =>
                {
                    var raw = bars.Select((b, i) =>
                    {
                        if (i + 1 < length) return -50;
                        var window = Window(bars, i, length).ToArray();
                        var low = window.Min(bar => bar.Low);
                        var high = window.Max(bar => bar.High);
                        return high == low ? -50 : 100 * ((b.Close - low) / (high - low) - 1); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }).ToArray();
                    var gains = Enumerable.Repeat(2d / (Integer(options, "SmoothLength", 3) + 1), bars.Count).ToArray();
                    return Outputs(("Swr", ExpandedGainTrajectory(raw, gains)));
                });
            case IndicatorName.ReverseMovingAverageConvergenceDivergence:
                return new("Rmacd", new[] { "Rmacd", "Signal", "Histogram" }, bars =>
                {
                    var fastPeriod = Integer(options, "FastLength", 12);
                    var slowPeriod = Integer(options, "SlowLength", 26);
                    var prices = Closes(bars);
                    var fast = Average(prices, fastPeriod, 3);
                    var slow = Average(prices, slowPeriod, 3);
                    var alphaFast = 2d / (fastPeriod + 1);
                    var alphaSlow = 2d / (slowPeriod + 1);
                    // Solve for the next price that leaves the difference of the two EMAs unchanged.
                    // Equal periods retain the common prior average as their degenerate equilibrium.
                    var line = prices.Select((_, i) => i == 0 ? 0 : alphaFast == alphaSlow ? fast[i - 1] // NOSONAR: S1244 - Equal gains define the degenerate equilibrium; unequal gains use the quotient.
                        : fast[i - 1] + alphaSlow * (fast[i - 1] - slow[i - 1]) / (alphaFast - alphaSlow)).ToArray();
                    var signal = Average(line, 9, 3);
                    return Outputs(("Rmacd", line), ("Signal", signal), ("Histogram", line.Zip(signal, (v, s) => v - s).ToArray()));
                });
            case IndicatorName.EnhancedWilliamsR:
                var enhancedKind = AverageKind(options, 1); if (enhancedKind == 0) return null;
                return new("Ewr", new[] { "Ewr", "Signal" }, bars => EnhancedWilliamsOutputs(bars, indicator));
            case IndicatorName.EhlersAMDetector:
                var envelopeKind = AverageKind(options, 1);
                if (envelopeKind == 0) return null;
                return new("Eamd", new[] { "Eamd", "Signal" }, bars =>
                {
                    var envelope = bars.Select((_, i) => Window(bars, i, Integer(options, "Length1", 4))
                        .Max(bar => Math.Abs(bar.Close - bar.Open))).ToArray();
                    var period = Integer(options, "Length2", 8);
                    var amplitude = Average(envelope, period, envelopeKind);
                    return Outputs(("Eamd", amplitude), ("Signal", Average(amplitude, period, envelopeKind)));
                });
            case IndicatorName.RexOscillator:
                return new("Ro", new[] { "Ro", "Signal" }, bars =>
                {
                    var votes = bars.Select(b => (b.Close - b.Open) + (b.Close - b.High) + (b.Close - b.Low)).ToArray();
                    var line = Average(votes, length, kind);
                    return Outputs(("Ro", line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.Repulse:
                return new("Repulse", new[] { "Repulse", "Signal" }, bars =>
                {
                    // Linearity combines bull minus bear power before smoothing.
                    var force = bars.Select((bar, i) =>
                    {
                        var window = Window(bars, i, length).ToArray();
                        var previousOpen = i == 0 ? 0 : bars[i - 1].Open;
                        return bar.Close == 0 ? 0 : 200 * (3 * bar.Close - window.Min(b => b.Low)
                            - window.Max(b => b.High) - previousOpen) / bar.Close;
                    }).ToArray();
                    var line = Average(force, length * 5, kind);
                    return Outputs(("Repulse", line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.MidpointOscillator:
                return new("Mo", new[] { "Mo", "Signal" }, bars =>
                {
                    var line = bars.Select((bar, i) =>
                    {
                        var window = Window(bars, i, length).ToArray();
                        var lower = window.Min(b => b.Low);
                        var upper = window.Max(b => b.High);
                        return upper == lower ? 0 : Math.Max(-100, Math.Min(100, 200 * (bar.Close - lower) / (upper - lower) - 100)); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }).ToArray();
                    return Outputs(("Mo", line), ("Signal", Average(line, 9, kind)));
                });
            case IndicatorName.ReallySimpleIndicator:
                return new("Rsi", new[] { "Rsi", "Signal" }, bars =>
                {
                    var baseline = Average(Closes(bars), length, kind);
                    var line = bars.Select((bar, i) => bar.Close == 0 ? 0 : 100 * (bar.Low - baseline[i]) / bar.Close).ToArray();
                    return Outputs(("Rsi", line), ("Signal", Average(line, Integer(options, "SmoothLength", 10), kind)));
                });
            case IndicatorName.RapidRelativeStrengthIndex:
                if (kind is 1 or 2 or 3 or 6) return new("Rrsi", new[] { "Rrsi", "Signal" }, bars => AdaptiveGainLossOutputs(bars, indicator));
                return new("Rrsi", new[] { "Rrsi", "Signal" }, bars =>
                {
                    var change = bars.Select((bar, i) => i == 0 ? 0 : bar.Close - bars[i - 1].Close).ToArray();
                    var line = change.Select((_, i) =>
                    {
                        var window = Window(change, i, length).ToArray();
                        var movement = window.Sum(Math.Abs);
                        // RSI = 50 * (1 + net movement / total movement), except the flat convention.
                        return movement == 0 ? 100 : 50 * (1 + window.Sum() / movement);
                    }).ToArray();
                    return Outputs(("Rrsi", line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.SigmaSpikes:
                return new("Ss", new[] { "Ss", "Signal" }, bars =>
                {
                    var returns = bars.Select((bar, i) => i == 0 || bars[i - 1].Close == 0 ? 0
                        : bar.Close / bars[i - 1].Close - 1).ToArray();
                    var variance = PopulationVariance(returns, length);
                    var line = returns.Select((value, i) => i == 0 || variance[i - 1] == 0 ? 0 : value / Math.Sqrt(variance[i - 1])).ToArray();
                    return Outputs(("Ss", line), ("Signal", Average(line, length, kind)));
                });
            default: return null;
        }
    }
}
