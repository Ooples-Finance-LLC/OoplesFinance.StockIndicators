using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? Oscillators(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var name = indicator.BatchName;
        var length = Integer(options, "Length", 14);
        var kind = AverageKind(options, 3);
        switch (name)
        {
            case IndicatorName.NormalizedRelativeVigorIndex:
                var symmetric = options.GetType().GetProperty("MaType")?.GetValue(options) is MovingAvgType.SymmetricallyWeightedMovingAverage;
                if (kind == 0 && !symmetric) return null;
                return new("Nrvi", new[] { "Nrvi", "Signal" }, bars =>
                {
                    double[] Smooth(double[] values)
                    {
                        if (!symmetric) return Average(values, length, kind);
                        var weights = Enumerable.Range(0, length).Select(j => (double)Math.Min(j + 1, length - j)).ToArray();
                        return values.Select((_, i) => Enumerable.Range(0, Math.Min(length, i + 1))
                            .Sum(j => weights[j] * values[i - j]) / weights.Sum()).ToArray();
                    }
                    var body = Smooth(bars.Select(b => b.Close - b.Open).ToArray());
                    var range = Smooth(bars.Select(b => b.High - b.Low).ToArray());
                    var line = body.Select((_, i) =>
                    {
                        var totalRange = Window(range, i, length).Sum();
                        return totalRange == 0 ? 0 : 100 * Window(body, i, length).Sum() / totalRange;
                    }).ToArray();
                    return Outputs(("Nrvi", line), ("Signal", Smooth(line)));
                });
            case IndicatorName.VaradiOscillator:
                var varadiKind = AverageKind(options, 1);
                if (varadiKind == 0) return null;
                return new("Vo", new[] { "Vo" }, bars =>
                {
                    var ratios = bars.Select(b => b.High + b.Low == 0 ? 0 : 2 * b.Close / (b.High + b.Low)).ToArray();
                    var average = Average(ratios, length, varadiKind);
                    // Rank against preceding observations; the available startup history includes a zero seed.
                    var line = average.Select((a, i) => 100d / length * Enumerable.Range(Math.Max(-1, i - length), Math.Min(i + 1, length))
                        .Count(j => (j < 0 ? 0 : average[j]) <= a + 1e-12 * Math.Max(1, Math.Abs(a)))).ToArray();
                    return Outputs(("Vo", line));
                });
            case IndicatorName.OceanIndicator:
                if (kind == 0) return null;
                return new("Oi", new[] { "Oi", "Signal" }, bars =>
                {
                    // Published scale is 100,000 / sqrt(period); unavailable/nonpositive log prices use zero.
                    var logs = bars.Select(b => b.Close > 0 ? Math.Log(b.Close) : 0).ToArray();
                    var line = logs.Select((v, i) => 100000 / Math.Sqrt(length) * (v - (i < length ? 0 : logs[i - length]))).ToArray();
                    return Outputs(("Oi", line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.StochasticMomentumIndex:
                if (kind == 0) return null;
                return new("Smi", new[] { "Smi", "Signal" }, bars =>
                {
                    var period = Math.Max(1, Integer(options, "Length1", 2));
                    var aboveLow = bars.Select((b, i) => b.Close - Window(bars, i, period).Min(v => v.Low)).ToArray();
                    var belowHigh = bars.Select((b, i) => Window(bars, i, period).Max(v => v.High) - b.Close).ToArray();
                    double[] Smooth(double[] values) => Average(Average(values, Integer(options, "Length2", 8), kind),
                        Integer(options, "SmoothLength1", 5), kind);
                    var up = Smooth(aboveLow);
                    var down = Smooth(belowHigh);
                    var line = up.Zip(down, (u, d) => u + d == 0 ? 0 : Math.Max(-100, Math.Min(100, 100 * (u - d) / (u + d)))).ToArray();
                    return Outputs(("Smi", line), ("Signal", Average(line, Integer(options, "SmoothLength2", 5), kind)));
                });
            case IndicatorName.SMIErgodicIndicator:
                if (kind == 0) return null;
                return new("Smi", new[] { "Smi", "Signal" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    double[] Smooth(double[] values) => Average(Average(values, Integer(options, "FastLength", 5), kind),
                        Integer(options, "SlowLength", 20), kind);
                    var up = Smooth(changes.Select(v => Math.Max(0, v)).ToArray());
                    var down = Smooth(changes.Select(v => Math.Max(0, -v)).ToArray());
                    var line = up.Zip(down, (u, d) => u + d == 0 ? 0 : Math.Max(-100, Math.Min(100, 100 * (u - d) / (u + d)))).ToArray();
                    return Outputs(("Smi", line), ("Signal", Average(line, Integer(options, "SignalLength", 5), kind)));
                });
            case IndicatorName.PremierStochasticOscillator:
                if (kind == 0) return null;
                return new("Pso", new[] { "Pso" }, bars =>
                {
                    var stochastic = bars.Select((b, i) =>
                    {
                        var window = Window(bars, i, length).ToArray();
                        var low = window.Min(v => v.Low);
                        var high = window.Max(v => v.High);
                        return high == low ? -5 : 10 * (b.Close - low) / (high - low) - 5; // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }).ToArray();
                    var period = Math.Max(2, Math.Min(530, (int)Math.Ceiling(Math.Sqrt(Integer(options, "SmoothLength", 25)))));
                    var smoothed = Average(Average(stochastic, period, kind), period, kind);
                    return Outputs(("Pso", smoothed.Select(v => Math.Tanh(v / 2)).ToArray()));
                });
            case IndicatorName.SchaffTrendCycleShk:
                if (kind == 0) return null;
                return new("Stc", new[] { "Stc", "Macd" }, bars =>
                {
                    var prices = Closes(bars).Select(BinaryDecimal).ToArray();
                    var fast = MotionDecimalAverage(prices, Integer(options, "FastLength"), kind);
                    var slow = MotionDecimalAverage(prices, Integer(options, "SlowLength"), kind);
                    var macd = fast.Select((v, i) => v-slow[i]).ToArray();
                    var period = Integer(options, "CycleLength");
                    decimal[] Normalize(decimal[] source, decimal[] scale)
                    {
                        var readings = new List<(int Index, decimal Value)> { (-1, 0) };
                        return source.Select((v, i) =>
                        {
                            var window = Window(source, i, period).ToArray();
                            var low = window.Min(); var high = window.Max();
                            if (high-low > 0.000000000000014210854715202004m*Window(scale, i, period).Max())
                                readings.Add((i, Clamp(100*(v-low)/(high-low), 0, 100)));
                            return readings[readings.Count-1].Value;
                        }).ToArray();
                    }
                    decimal[] Smooth(decimal[] values, int smoothing)
                    {
                        var gain = 2m / (smoothing + 1);
                        var powers = new decimal[values.Length + 1]; powers[0] = 1;
                        for (var i = 1; i < powers.Length; i++) powers[i] = powers[i - 1] * (1 - gain);
                        return values.Select((_, i) => Enumerable.Range(0, i + 1)
                            .Sum(j => values[j] * gain * powers[i - j])).ToArray();
                    }
                    var first = Normalize(macd, fast.Select((v, i) => Math.Abs(v)+Math.Abs(slow[i])).ToArray());
                    var middle = Smooth(first, Integer(options, "D1Length"));
                    var second = Normalize(middle, middle.Select(Math.Abs).ToArray());
                    return Outputs(("Stc", Smooth(second, Integer(options, "D2Length")).Select(v => (double)v).ToArray()),
                        ("Macd", macd.Select(v => (double)v).ToArray()));
                });
            case IndicatorName.SchaffTrendCycle:
                if (kind == 0) return null;
                return new("Stc", new[] { "Stc" }, bars =>
                {
                    // This legacy variant publishes the first stochastic pass. The separate SHK variant
                    // implements the double stochastic/smoothing construction.
                    var prices = Closes(bars);
                    var fast = Average(prices, Integer(options, "FastLength", 23), kind);
                    var slow = Average(prices, Integer(options, "SlowLength", 50), kind);
                    var macd = fast.Zip(slow, (f, s) => f - s).ToArray();
                    var period = Math.Max(1, Integer(options, "CycleLength", length));
                    var line = macd.Select((v, i) =>
                    {
                        var window = Window(macd, i, period).ToArray();
                        var lower = window.Min();
                        var upper = window.Max();
                        var scale = Enumerable.Range(Math.Max(0, i - period + 1), Math.Min(period, i + 1))
                            .Max(j => Math.Abs(fast[j]) + Math.Abs(slow[j]));
                        // Numerical contract: unresolved MACD ranges (64 machine epsilons of the averages) are flat.
                        return upper - lower <= 64 * Math.Pow(2, -52) * scale ? 0 : 100 * (v - lower) / (upper - lower);
                    }).ToArray();
                    return Outputs(("Stc", line));
                });
            case IndicatorName.BearPowerIndicator:
            case IndicatorName.BullPowerIndicator:
                if (kind == 0) return null;
                var buyerPower = name == IndicatorName.BullPowerIndicator;
                var powerKey = buyerPower ? "BullPower" : "BearPower";
                return new(powerKey, new[] { powerKey, "Signal" }, bars =>
                {
                    // Partition valid OHLC candles by body direction, then opening gap and wick lengths.
                    // These are the library's candle-power definitions, separate from Elder Ray power.
                    var line = bars.Select((b, i) =>
                    {
                        var previous = i == 0 ? 0 : bars[i - 1].Close;
                        var range = b.High - b.Low;
                        var upper = b.High - b.Close;
                        var lower = b.Close - b.Low;
                        if (buyerPower)
                        {
                            if (b.Close < b.Open) return Math.Max(b.High - b.Open, lower);
                            if (previous < b.Open) return Math.Max(b.High - previous, lower);
                            if (b.Close > b.Open || previous > b.Open) return range;
                            return upper > lower ? upper : range;
                        }
                        if (b.Close < b.Open || previous > b.Open) return range;
                        if (b.Close > b.Open) return Math.Max(b.Open - b.Low, upper);
                        if (upper > lower) return range;
                        if (upper < lower || previous < b.Open) return lower;
                        return range;
                    }).ToArray();
                    return Outputs((powerKey, line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.GainLossMovingAverage:
                kind = AverageKind(options, 6);
                if (kind == 0) return null;
                var gainLossSignal = Integer(options, "SignalLength", 7);
                return new("Glma", new[] { "Glma", "Signal" }, bars =>
                {
                    var symmetricReturns = bars.Select((b, i) => i == 0 || b.Close + bars[i - 1].Close == 0 ? 0
                        : 200 * (b.Close - bars[i - 1].Close) / (b.Close + bars[i - 1].Close)).ToArray();
                    var line = Average(symmetricReturns, length, kind);
                    return Outputs(("Glma", line), ("Signal", Average(line, gainLossSignal, kind)));
                });
            case IndicatorName.ElderMarketThermometer:
                if (kind == 0) return null;
                return new("Emt", new[] { "Emt", "Signal" }, bars =>
                {
                    var expansion = bars.Select((b, i) => Math.Max(0, Math.Max(
                        b.High - (i == 0 ? 0 : bars[i - 1].High),
                        (i == 0 ? 0 : bars[i - 1].Low) - b.Low))).ToArray();
                    return Outputs(("Emt", expansion), ("Signal", Average(expansion, length, kind)));
                });
            case IndicatorName.ErgodicMeanDeviationIndicator:
                if (kind == 0) return null;
                var meanFirst = Integer(options, "Length1", 32);
                var meanSecond = Integer(options, "Length2", 5);
                var meanThird = Integer(options, "Length3", 5);
                var meanSignal = Integer(options, "SignalLength", 5);
                return new("Emdi", new[] { "Emdi", "Signal" }, bars =>
                {
                    var mean = Average(Closes(bars), meanFirst, kind);
                    var residual = bars.Select((b, i) => b.Close - mean[i]).ToArray();
                    var line = Average(Average(residual, meanSecond, kind), meanThird, kind);
                    return Outputs(("Emdi", line), ("Signal", Average(line, meanSignal, kind)));
                });
            case IndicatorName.ErgodicTrueStrengthIndexV2 when kind is 1 or 2 or 3 or 6:
                return new("Etsi2", new[] { "Etsi1", "Etsi2", "Signal" }, bars => StrengthOutputs(bars, indicator));
            case IndicatorName.ErgodicTrueStrengthIndexV1 when kind is 1 or 2 or 3 or 6:
                return new("Etsi", new[] { "Etsi", "Signal" }, bars => StrengthOutputs(bars, indicator));
            case IndicatorName.ErgodicTrueStrengthIndexV1:
            case IndicatorName.ErgodicTrueStrengthIndexV2:
                if (kind == 0) return null;
                var twoStrengths = name == IndicatorName.ErgodicTrueStrengthIndexV2;
                var strengthSignal = Integer(options, "SignalLength", 3);
                return new(twoStrengths ? "Etsi2" : "Etsi", twoStrengths ? new[] { "Etsi1", "Etsi2", "Signal" }
                    : new[] { "Etsi", "Signal" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    double[] Strength(int offset)
                    {
                        var signed = changes;
                        var absolute = changes.Select(Math.Abs).ToArray();
                        for (var stage = 1; stage <= 3; stage++)
                        {
                            var period = Integer(options, "Length" + (offset + stage), 1);
                            signed = Average(signed, period, kind);
                            absolute = Average(absolute, period, kind);
                        }
                        return signed.Select((v, i) => absolute[i] == 0 ? 0 :
                            Math.Max(-100, Math.Min(100, 100 * v / absolute[i]))).ToArray();
                    }
                    var first = Strength(0);
                    if (!twoStrengths) return Outputs(("Etsi", first), ("Signal", Average(first, strengthSignal, kind)));
                    var second = Strength(3);
                    return Outputs(("Etsi1", first), ("Etsi2", second), ("Signal", Average(second, strengthSignal, kind)));
                });
            case IndicatorName.ErgodicCandlestickOscillator:
                if (kind == 0) return null;
                return new("Eco", new[] { "Eco", "Signal" }, bars =>
                {
                    var bodies = bars.Select(b => b.Close - b.Open).ToArray();
                    var ranges = bars.Select(b => b.High - b.Low).ToArray();
                    var numerator = Average(Average(bodies, 32, kind), length, kind);
                    var denominator = Average(Average(ranges, 32, kind), length, kind);
                    var line = numerator.Select((v, i) => denominator[i] == 0 ? 0 : 100 * v / denominator[i]).ToArray();
                    return Outputs(("Eco", line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.EnhancedIndex:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                var enhancedSignal = Integer(options, "SignalLength", 8);
                var enhancedMean = Math.Max(2, Math.Min(530, (length + 1) / 2));
                return new("Ei", new[] { "Ei", "Signal" }, bars =>
                {
                    var mean = Average(Closes(bars), enhancedMean, kind);
                    var line = bars.Select((b, i) =>
                    {
                        var window = Window(bars, i, length).ToArray();
                        var range = window.Max(v => v.High) - window.Min(v => v.Low);
                        return range == 0 ? 0 : 2 * (b.Close - mean[i]) / range;
                    }).ToArray();
                    return Outputs(("Ei", line), ("Signal", Average(line, enhancedSignal, kind)));
                });
            case IndicatorName.EhlersRelativeVigorIndex:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                var vigorSignal = Integer(options, "SignalLength", 4);
                return new("Ervi", new[] { "Ervi", "Signal" }, bars =>
                {
                    var proportions = bars.Select(b => b.High == b.Low ? 0 : (b.Close - b.Open) / (b.High - b.Low)).ToArray(); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    var line = Average(proportions, length, kind);
                    return Outputs(("Ervi", line), ("Signal", Average(line, vigorSignal, kind)));
                });
            case IndicatorName.EhlersFisherTransform:
                return new("Eft", new[] { "Eft" }, bars =>
                {
                    var prices = Closes(bars);
                    var transformed = new double[bars.Count];
                    double normalized = 0;
                    for (var i = 0; i < transformed.Length; i++)
                    {
                        var window = Window(prices, i, length).ToArray();
                        var low = window.Min();
                        var high = window.Max();
                        var position = high == low ? 0 : 2 * (prices[i] - low) / (high - low) - 1; // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                        normalized = Math.Max(-.999, Math.Min(.999, normalized + .33 * (position - normalized)));
                        transformed[i] = .5 * (Math.Log(1 + normalized) - Math.Log(1 - normalized));
                    }
                    // The final one-pole stage has a geometric impulse response, seeded at zero.
                    var line = transformed.Select((_, i) => Enumerable.Range(0, i + 1)
                        .Sum(j => Math.Pow(.5, i - j) * transformed[j])).ToArray();
                    return Outputs(("Eft", line));
                });
            case IndicatorName.EhlersInverseFisherTransform:
                kind = AverageKind(options, 2);
                if (kind == 0) return null;
                var inverseRsiLength = options is OoplesFinance.StockIndicators.Builder.Specs.InverseFisherTransformCoreSpecOptions ? 5 : length;
                return new("Eift", new[] { "Eift" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var gains = Average(changes.Select(v => Math.Max(v, 0)).ToArray(), inverseRsiLength, kind);
                    var losses = Average(changes.Select(v => Math.Max(-v, 0)).ToArray(), inverseRsiLength, kind);
                    var rescaled = gains.Select((v, i) => losses[i] == 0 ? 5 : 10 * v / (v + losses[i]) - 5).ToArray();
                    return Outputs(("Eift", Average(rescaled, 9, kind).Select(Math.Tanh).ToArray()));
                });
            case IndicatorName.EhlersMovingAverageDifferenceIndicator:
                kind = AverageKind(options, 2);
                if (kind == 0) return null;
                var differenceFast = Integer(options, "FastLength", 8);
                var differenceSlow = Integer(options, "SlowLength", 23);
                return new("Emad", new[] { "Emad" }, bars =>
                {
                    var fast = Average(Closes(bars), differenceFast, kind);
                    var slow = Average(Closes(bars), differenceSlow, kind);
                    return Outputs(("Emad", fast.Select((v, i) => slow[i] == 0 ? 0 : 100 * (v / slow[i] - 1)).ToArray()));
                });
            case IndicatorName.DecisionPointPriceMomentumOscillator:
                if (kind is 1 or 2 or 3 or 6) return new("Dppmo", new[] { "Dppmo", "Signal", "Histogram" }, bars => RocPipelineOutputs(bars, indicator));
                if (kind == 0) return null;
                var pmoFirst = 2 * length + 7;
                var pmoSecond = length + 6;
                var pmoSignal = Integer(options, "SignalLength", 10);
                return new("Dppmo", new[] { "Dppmo", "Signal", "Histogram" }, bars =>
                {
                    var returns = bars.Select((b, i) => i == 0 || bars[i - 1].Close == 0 ? 0 :
                        100 * (b.Close - bars[i - 1].Close) / bars[i - 1].Close).ToArray();
                    // PMO uses 2/period, rather than the EMA's 2/(period+1), with zero initial state.
                    double[] Convolve(double[] input, int period) => input.Select((_, i) =>
                        Enumerable.Range(0, i + 1).Sum(j => 2d / period * Math.Pow(1 - 2d / period, i - j) * input[j])).ToArray();
                    var line = Convolve(Convolve(returns, pmoFirst), pmoSecond).Select(v => 10 * v).ToArray();
                    return Oscillator("Dppmo", line, Average(line, pmoSignal, kind));
                });
            case IndicatorName.DerivativeOscillator:
                if (kind == 0) return null;
                var derivativeRsi = length;
                var derivativeMean = Integer(options, "Length2", 9);
                var derivativeFirst = Integer(options, "Length3", 5);
                var derivativeSecond = Integer(options, "Length4", 3);
                return new("Do", new[] { "Do" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var up = Average(changes.Select(v => Math.Max(0, v)).ToArray(), derivativeRsi, kind);
                    var down = Average(changes.Select(v => Math.Max(0, -v)).ToArray(), derivativeRsi, kind);
                    var rsi = up.Select((v, i) => down[i] == 0 ? 100 : 100 * v / (v + down[i])).ToArray();
                    var smoothed = Average(Average(rsi, derivativeFirst, kind), derivativeSecond, kind);
                    return Outputs(("Do", smoothed.Select((v, i) => v - Window(smoothed, i, derivativeMean).Average()).ToArray()));
                });
            case IndicatorName.DoubleSmoothedStochastic:
                if (kind == 0) return null;
                return new("Dss", new[] { "Dss", "Signal" }, bars =>
                {
                    var lows = bars.Select((_, i) => Window(bars, i, length).Min(b => b.Low)).ToArray();
                    var widths = bars.Select((_, i) => Window(bars, i, length).Max(b => b.High) - lows[i]).ToArray();
                    var offsets = bars.Select((b, i) => b.Close - lows[i]).ToArray();
                    double[] Smooth(double[] values) => Average(Average(values, 3, kind), 15, kind);
                    var numerator = Smooth(offsets);
                    var denominator = Smooth(widths);
                    var line = numerator.Select((v, i) => denominator[i] == 0 ? 0 : Math.Max(0, Math.Min(100, 100 * v / denominator[i]))).ToArray();
                    return Outputs(("Dss", line), ("Signal", Average(line, 3, kind)));
                });
            case IndicatorName.DoubleStochasticOscillator:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Dso", new[] { "Dso", "Signal" }, bars =>
                {
                    var stochastic = bars.Select((b, i) =>
                    {
                        var window = Window(bars, i, length).ToArray();
                        var minimum = window.Min(v => v.Low);
                        var width = window.Max(v => v.High) - minimum;
                        return width == 0 ? 0 : 100 * (b.Close - minimum) / width;
                    }).ToArray();
                    var reranged = stochastic.Select((v, i) =>
                    {
                        var window = Window(stochastic, i, Math.Max(2, length)).ToArray();
                        var minimum = window.Min();
                        var width = window.Max() - minimum;
                        return width == 0 ? 0 : 100 * (v - minimum) / width;
                    }).ToArray();
                    var line = Average(reranged, 3, kind);
                    return Outputs(("Dso", line), ("Signal", Average(line, 3, kind)));
                });
            case IndicatorName.DoubleSmoothedMomenta:
                if (kind is 1 or 2 or 3 or 6) return new("Dsm", new[] { "Dsm", "Signal" }, bars => MomentaOutputs(bars, indicator));
                if (kind == 0) return null;
                var momentaRange = Math.Max(2, Integer(options, "MomentumLength", 2));
                var momentaFirst = Integer(options, "FirstSmooth", 5);
                var momentaSecond = Integer(options, "SecondSmooth", 25);
                return new("Dsm", new[] { "Dsm", "Signal" }, bars =>
                {
                    var prices = Closes(bars);
                    var lows = prices.Select((_, i) => Window(prices, i, momentaRange).Min()).ToArray();
                    var range = prices.Select((_, i) => Window(prices, i, momentaRange).Max() - lows[i]).ToArray();
                    var displacement = prices.Select((v, i) => v - lows[i]).ToArray();
                    double[] Smooth(double[] values) => Average(Average(values, momentaFirst, kind), momentaSecond, kind);
                    var numerator = Smooth(displacement);
                    var denominator = Smooth(range);
                    var line = numerator.Select((v, i) => denominator[i] == 0 ? 0 : Math.Max(0, Math.Min(100, 100 * v / denominator[i]))).ToArray();
                    return Outputs(("Dsm", line), ("Signal", Average(line, momentaSecond, kind)));
                });
            case IndicatorName.BayesianOscillator:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("SigmaProbsDown", new[] { "SigmaProbsDown", "SigmaProbsUp", "ProbPrime" }, bars =>
                {
                    var mean = Average(Closes(bars), length, kind);
                    var variance = PopulationVariance(Closes(bars), length);
                    var upperSigns = bars.Select((b, i) => Math.Sign(b.Close - mean[i] - 2.5 * Math.Sqrt(variance[i]))).ToArray();
                    var meanSigns = bars.Select((b, i) => Math.Sign(b.Close - mean[i])).ToArray();
                    // Combine rational evidence masses directly. Empty evidence has probability zero.
                    double[] Evidence(int sign) => bars.Select((_, i) =>
                    {
                        var upper = Window(upperSigns, i, length).ToArray();
                        var basis = Window(meanSigns, i, length).ToArray();
                        var first = upper.Count(v => v == sign);
                        var second = basis.Count(v => v == sign);
                        var firstTotal = Math.Max(1, upper.Count(v => v != 0));
                        var secondTotal = Math.Max(1, basis.Count(v => v != 0));
                        var joint = first * second;
                        var opposite = (firstTotal - first) * (secondTotal - second);
                        return joint + opposite == 0 ? 0 : (double)joint / (joint + opposite);
                    }).ToArray();
                    var down = Evidence(1);
                    var up = Evidence(-1);
                    var prime = down.Select((d, i) =>
                    {
                        var joint = d * up[i];
                        var total = joint + (1 - d) * (1 - up[i]);
                        return total == 0 ? 0 : joint / total;
                    }).ToArray();
                    return Outputs(("SigmaProbsDown", down), ("SigmaProbsUp", up), ("ProbPrime", prime));
                });
            case IndicatorName.FractalChaosOscillator:
                return new("Fco", new[] { "Fco" }, bars =>
                {
                    var result = new double[bars.Count];
                    double upper = 0, lower = 0;
                    for (var i = 4; i < result.Length; i++)
                    {
                        var window = Window(bars, i, 5).ToArray();
                        var center = window[2];
                        var peers = window.Where((_, j) => j != 2).ToArray();
                        var nextUpper = peers.All(b => b.High < center.High) ? center.High : upper;
                        var nextLower = peers.All(b => b.Low > center.Low) ? center.Low : lower;
                        result[i] = nextUpper != upper ? 1 : nextLower != lower ? -1 : 0; // NOSONAR: S1244 - Breakout direction depends on a bound actually changing.
                        upper = nextUpper;
                        lower = nextLower;
                    }
                    return Outputs(("Fco", result));
                });
            case IndicatorName.FoldedRelativeStrengthIndex:
                if (kind is 1 or 2 or 3 or 6) return new("Frsi", new[] { "Frsi", "Signal" }, bars => FoldedRsiOutputs(bars, indicator));
                if (kind == 0) return null;
                return new("Frsi", new[] { "Frsi", "Signal" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var up = Average(changes.Select(v => Math.Max(v, 0)).ToArray(), length, kind);
                    var down = Average(changes.Select(v => Math.Max(-v, 0)).ToArray(), length, kind);
                    // Folded RSI = absolute normalized gain/loss imbalance.
                    var folded = up.Select((v, i) => v + down[i] == 0 ? 100 : 100 * Math.Abs(v - down[i]) / (v + down[i])).ToArray();
                    var line = folded.Select((_, i) => Window(folded, i, length).Sum()).ToArray();
                    return Outputs(("Frsi", line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.ForecastOscillator:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Fo", new[] { "Fo", "Signal" }, bars =>
                {
                    var line = bars.Select((b, i) => i == 0 || b.Close == 0 ? 0 : 100 * (1 - bars[i - 1].Close / b.Close)).ToArray();
                    return Outputs(("Fo", line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.DeltaMovingAverage:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                var deltaLag = Integer(options, "Length2", 5);
                var deltaSmooth = Integer(options, "Length1", 10);
                return new("Delta", new[] { "Delta", "Signal", "Histogram" }, bars =>
                {
                    var line = bars.Select((b, i) => b.Close - (i < deltaLag ? 0 : bars[i - deltaLag].Open)).ToArray();
                    return Oscillator("Delta", line, Average(line, deltaSmooth, kind));
                });
            case IndicatorName.DampingIndex:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Di", new[] { "Di" }, bars =>
                {
                    var range = Average(bars.Select(b => b.High - b.Low).ToArray(), length, kind);
                    return Outputs(("Di", range.Select((_, i) => i < 6 || range[i - 6] == 0 ? 0 : range[i - 1] / range[i - 6]).ToArray()));
                });
            case IndicatorName.Demarker:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Dm", new[] { "Dm" }, bars =>
                {
                    var up = Average(bars.Select((b, i) => i == 0 ? 0 : Math.Max(0, b.High - bars[i - 1].High)).ToArray(), length, kind);
                    var down = Average(bars.Select((b, i) => i == 0 ? 0 : Math.Max(0, bars[i - 1].Low - b.Low)).ToArray(), length, kind);
                    return Outputs(("Dm", up.Select((v, i) => v + down[i] == 0 ? 0 : 100 * v / (v + down[i])).ToArray()));
                });
            case IndicatorName.ConstanceBrownCompositeIndex:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                var brownRsiLength = Integer(options, "Length1", 14);
                var brownMomentumLength = Integer(options, "Length2", 9);
                var brownSmooth = Integer(options, "SmoothLength", 3);
                var brownFast = Integer(options, "FastLength", 13);
                var brownSlow = Integer(options, "SlowLength", 33);
                return new("Cbci", new[] { "Cbci", "FastSignal", "SlowSignal" }, bars =>
                {
                    double[] WilderRsi(int period)
                    {
                        var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                        var gains = Average(changes.Select(v => Math.Max(v, 0)).ToArray(), period, 6);
                        var losses = Average(changes.Select(v => Math.Max(-v, 0)).ToArray(), period, 6);
                        return gains.Select((v, i) => losses[i] == 0 ? 100 : 100 * v / (v + losses[i])).ToArray();
                    }
                    var rsi = WilderRsi(brownRsiLength);
                    var level = Average(WilderRsi(brownSmooth), brownSmooth, kind);
                    var line = rsi.Select((v, i) => level[i] + (i < brownMomentumLength ? 0 : v - rsi[i - brownMomentumLength])).ToArray();
                    return Outputs(("Cbci", line), ("FastSignal", Average(line, brownFast, kind)), ("SlowSignal", Average(line, brownSlow, kind)));
                });
            case IndicatorName.ChandeMomentumOscillatorAverageDisparityIndex:
                return new("Cmoadi", new[] { "Cmoadi" }, bars =>
                {
                    var prices = Closes(bars);
                    var averages = new[] { 200, 50, 20 }.Select(p => Average(prices, p, 3)).ToArray();
                    return Outputs(("Cmoadi", prices.Select((v, i) => v == 0 ? 0 : 100 * (1 - averages.Average(a => a[i]) / v)).ToArray()));
                });
            case IndicatorName.ConditionalAccumulator:
                if (kind == 0) return null;
                return new("Ca", new[] { "Ca", "Signal" }, bars =>
                {
                    var steps = bars.Select((b, i) => i == 0 ? 0d :
                        Convert.ToInt32(b.Low > bars[i - 1].High) - Convert.ToInt32(b.High < bars[i - 1].Low)).ToArray();
                    var line = Cumulative(steps);
                    return Outputs(("Ca", line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.ChopZone:
                if (kind == 0) return null;
                return new("Cz", new[] { "Cz" }, bars =>
                {
                    var average = Average(Closes(bars), 34, kind);
                    return Outputs(("Cz", bars.Select((b, i) =>
                    {
                        var window = Window(bars, i, length).ToArray();
                        var low = window.Min(v => v.Low);
                        var width = window.Max(v => v.High) - low;
                        var typical = (b.High + b.Low + b.Close) / 3;
                        var slope = width == 0 || typical == 0 ? 0 :
                            (average[i] - (i == 0 ? 0 : average[i - 1])) * 25 * low / (width * typical);
                        // atan(slope) is the signed angle of the unit horizontal segment.
                        return Math.Round(Math.Atan(slope) * 180 / Math.PI);
                    }).ToArray()));
                });
            case IndicatorName.ChandeCompositeMomentumIndex:
                return new("Ccmi", new[] { "Ccmi", "Signal" }, bars =>
                {
                    var prices = Closes(bars);
                    var changes = prices.Select((v, i) => i == 0 ? 0 : v - prices[i - 1]).ToArray();
                    var components = new[] { 5, 10, 20 }.Select(period =>
                    {
                        var momentum = changes.Select((_, i) =>
                        {
                            var window = Window(changes, i, period).ToArray();
                            var travel = window.Sum(Math.Abs);
                            return travel == 0 ? 0 : 100 * window.Sum() / travel;
                        }).ToArray();
                        return (Momentum: Average(momentum, 3, 4), Weight: PopulationVariance(prices, period).Select(Math.Sqrt).ToArray());
                    }).ToArray();
                    var composite = prices.Select((_, i) =>
                    {
                        var total = components.Sum(c => c.Weight[i]);
                        return total == 0 ? 0 : Clamp(components.Sum(c => c.Weight[i] * c.Momentum[i]) / total, -100, 100);
                    }).ToArray();
                    // Explicit exponential impulse weights for the zero-seeded final EMA(3).
                    var line = composite.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j => composite[j] * Math.Pow(.5, i - j + 1))).ToArray();
                    return Outputs(("Ccmi", line), ("Signal", composite.Select((_, i) => Window(composite, i, 5).Average()).ToArray()));
                });
            case IndicatorName.ChartmillValueIndicator:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Cmvc", new[] { "Cmvc", "Cmvo", "Cmvh", "Cmvl" }, bars =>
                {
                    var midpoint = Average(bars.Select(b => (b.High + b.Low) / 2).ToArray(), length, kind);
                    var atr = Average(TrueRanges(bars), length, kind);
                    double[] Normalize(Func<Bar, double> selector) => bars.Select((b, i) => atr[i] == 0 ? 0 :
                        Clamp((selector(b) - midpoint[i]) / (atr[i] * Math.Sqrt(length)), -1, 1)).ToArray();
                    return Outputs(("Cmvc", Normalize(b => b.Close)), ("Cmvo", Normalize(b => b.Open)),
                        ("Cmvh", Normalize(b => b.High)), ("Cmvl", Normalize(b => b.Low)));
                });
            case IndicatorName.ChandeMomentumOscillatorAbsolute:
                return new("Cmoa", new[] { "Cmoa" }, bars => Outputs(("Cmoa",
                    RoundedAbsoluteChande(bars, length))));
            case IndicatorName.ChandeMomentumOscillatorFilter:
                kind = AverageKind(options, 2);
                if (kind == 0) return null;
                return new("Cmof", new[] { "Cmof", "Signal" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close)
                        .Select(v => Math.Abs(v) > 3 ? 0 : v).ToArray();
                    var line = changes.Select((_, i) =>
                    {
                        var window = Window(changes, i, length).ToArray();
                        var gains = window.Sum(v => Math.Max(v, 0));
                        var losses = window.Sum(v => Math.Max(-v, 0));
                        return gains + losses == 0 ? 0 : 100 * (gains - losses) / (gains + losses);
                    }).ToArray();
                    return Outputs(("Cmof", line), ("Signal", Average(line, length, kind)));
                });
            case IndicatorName.ChandeMomentumOscillatorAverage:
            case IndicatorName.ChandeMomentumOscillatorAbsoluteAverage:
                var chandeKey = name == IndicatorName.ChandeMomentumOscillatorAverage ? "Cmoa" : "Cmoaa";
                return new(chandeKey, new[] { chandeKey }, bars => Outputs((chandeKey,
                    RoundedChandeAverage(bars, absolute: name == IndicatorName.ChandeMomentumOscillatorAbsoluteAverage))));
            case IndicatorName.CenterOfLinearity:
                return new("Col", new[] { "Col" }, bars => Outputs(("Col", bars.Select((_, i) =>
                {
                    // Collect each historical price's coefficient in the two shifted weighted sums.
                    var first = Math.Max(0, i - length + 1);
                    double value = 0;
                    for (var j = Math.Max(0, first - length); j < i; j++)
                    {
                        var positive = j + length >= first && j + length <= i ? j + length + 1 : 0;
                        var negative = j + 1 >= first && j + 1 <= i ? j + 2 : 0;
                        value += (positive - negative) * bars[j].Close;
                    }
                    return value;
                }).ToArray())));
            case IndicatorName.CCTStochRelativeStrengthIndex:
                if (kind == 0) return null;
                var cctPeriods = new[] { Integer(options, "Length1", 5), Integer(options, "Length2", 8),
                    Integer(options, "Length3", 13), Integer(options, "Length4", 14), Integer(options, "Length5", 21) };
                var cctSmooth = Integer(options, "SmoothLength1", 3);
                var cctSlowSmooth = Integer(options, "SmoothLength2", 8);
                var cctKeys = new[] { "Type1", "Type2", "Type3", "Type4", "Type5", "Type6", "TypeCustom", "Signal" };
                return new("Type1", cctKeys, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var strengths = cctPeriods.Select(period =>
                    {
                        var up = Average(changes.Select(v => Math.Max(v, 0)).ToArray(), period, kind);
                        var down = Average(changes.Select(v => Math.Max(-v, 0)).ToArray(), period, kind);
                        var strength = up.Select((v, i) => down[i] == 0 ? 100 : 100 * v / (v + down[i])).ToArray();
                        if (period > 1 && (kind == 3 || kind == 6))
                            for (var i = 1; i < strength.Length; i++) if (changes[i] == 0) strength[i] = strength[i - 1];
                        return strength;
                    }).ToArray();
                    double[] Stochastic(int source, int numeratorLow, int denominatorLow, int denominatorHigh)
                    {
                        var values = strengths[source];
                        return values.Select((v, i) =>
                        {
                            var width = Window(values, i, cctPeriods[denominatorHigh]).Max() - Window(values, i, cctPeriods[denominatorLow]).Min();
                            return width == 0 ? 0 : 100 * (v - Window(values, i, cctPeriods[numeratorLow]).Min()) / width;
                        }).ToArray();
                    }
                    var first = Stochastic(4, 1, 2, 2);
                    return Outputs(("Type1", first), ("Type2", Stochastic(4, 4, 4, 4)), ("Type3", Stochastic(3, 3, 3, 3)),
                        ("Type4", Average(Stochastic(4, 2, 2, 1), cctSlowSmooth, kind)),
                        ("Type5", Average(Stochastic(0, 0, 0, 0), cctSmooth, kind)),
                        ("Type6", Average(Stochastic(2, 2, 2, 2), cctSmooth, kind)),
                        ("TypeCustom", Average(Stochastic(1, 1, 1, 1), cctSmooth, kind)),
                        ("Signal", Average(first, 9, kind)));
                });
            case IndicatorName.BilateralStochasticOscillator:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Bso", new[] { "Bull", "Bear", "Bso", "Signal" }, bars =>
                {
                    var average = Average(Closes(bars), length, kind);
                    var highs = average.Select((_, i) => Window(average, i, Math.Max(2, length)).Max()).ToArray();
                    var lows = average.Select((_, i) => Window(average, i, Math.Max(2, length)).Min()).ToArray();
                    var scale = Average(highs.Select((v, i) => v - lows[i]).ToArray(), length, kind);
                    var bull = average.Select((v, i) => scale[i] == 0 ? 0 : (v - lows[i]) / scale[i]).ToArray();
                    var bear = average.Select((v, i) => scale[i] == 0 ? 0 : (highs[i] - v) / scale[i]).ToArray();
                    var line = bull.Select((v, i) => Math.Max(v, bear[i])).ToArray();
                    return Outputs(("Bull", bull), ("Bear", bear), ("Bso", line), ("Signal", Average(line, 20, kind)));
                });
            case IndicatorName.BreakoutRelativeStrengthIndex:
                var breakoutVolumeLength = Integer(options, "LbLength", 2);
                return new("Brsi", new[] { "Brsi" }, bars =>
                {
                    var power = bars.Select((b, i) => b.High == b.Low ? 0 : // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                        (b.Open + b.High + b.Low + b.Close) / 4 * (b.Close - b.Open) / (b.High - b.Low)
                        * Window(bars, i, breakoutVolumeLength).Sum(v => v.Volume)).ToArray();
                    var positive = power.Select((v, i) => v > (i == 0 ? 0 : power[i - 1]) ? Math.Abs(v) : 0).ToArray();
                    var negative = power.Select((v, i) => v < (i == 0 ? 0 : power[i - 1]) ? Math.Abs(v) : 0).ToArray();
                    return Outputs(("Brsi", power.Select((_, i) =>
                    {
                        var up = Window(positive, i, length).Sum();
                        var down = Window(negative, i, length).Sum();
                        return down == 0 ? 100 : 100 * up / (up + down);
                    }).ToArray()));
                });
            case IndicatorName.BelkhayateTiming:
                return new("Belkhayate", new[] { "Belkhayate" }, bars => Outputs(("Belkhayate", bars.Select((b, i) =>
                {
                    var window = Window(bars, i, 5).ToArray();
                    var width = window.Sum(v => v.High - v.Low);
                    // Five zero-padded midpoints, normalized by one fifth of mean range.
                    return width == 0 ? 0 : (25 * b.Close - 2.5 * window.Sum(v => v.High + v.Low)) / width;
                }).ToArray())));
            case IndicatorName.AverageAbsoluteErrorNormalization:
                return new("Aaen", new[] { "Aaen" }, bars =>
                {
                    var errors = new double[bars.Count];
                    var result = new double[bars.Count];
                    var prediction = bars.Count == 0 ? 0 : bars[0].Close;
                    for (var i = 0; i < result.Length; i++)
                    {
                        errors[i] = bars[i].Close - prediction;
                        var window = Window(errors, i, length).ToArray();
                        var net = window.Sum();
                        var travel = window.Sum(Math.Abs);
                        result[i] = travel == 0 ? 0 : Clamp(net / travel, -1, 1);
                        // Normalization cancels: a * mean(abs(error)) = mean(error).
                        prediction = bars[i].Close + net / window.Length;
                    }
                    return Outputs(("Aaen", result));
                });
            case IndicatorName.AsymmetricalRelativeStrengthIndex:
                return new("Arsi", new[] { "Arsi" }, bars => AdaptiveGainLossOutputs(bars, indicator));
            case IndicatorName.ApirineSlowRelativeStrengthIndex:
                kind = AverageKind(options, 6);
                if (kind is 1 or 2 or 3 or 6) return new("Asrsi", new[] { "Asrsi" }, bars => ApirineRsiOutputs(bars, indicator));
                if (kind == 0) return null;
                return new("Asrsi", new[] { "Asrsi" }, bars =>
                {
                    var smoothingPeriod = Integer(options, "SmoothLength", 6);
                    var average = Average(Closes(bars), smoothingPeriod, kind);
                    var residuals = bars.Select((b, i) => b.Close - average[i]).ToArray();
                    if (kind == 6)
                    {
                        // Closed impulse sum for (1-H_wilder) applied to prices. This keeps
                        // sub-ulp residuals that subtraction from the rounded average destroys.
                        var retention = 1 - 1d / smoothingPeriod;
                        residuals = bars.Select((_, i) => Enumerable.Range(0, i + 1).Sum(j =>
                            Math.Pow(retention, i - j + 1) * (bars[j].Close - (j == 0 ? 0 : bars[j - 1].Close)))).ToArray();
                    }
                    var positive = Average(residuals.Select(v => Math.Max(0, v)).ToArray(), length, kind);
                    var negative = Average(residuals.Select(v => Math.Max(0, -v)).ToArray(), length, kind);
                    return Outputs(("Asrsi", positive.Select((v, i) => negative[i] == 0 ? 100 : 100 * v / (v + negative[i])).ToArray()));
                });
            case IndicatorName.AdaptiveStochastic:
                var fastSpan = Integer(options, "MinLength", 50);
                var slowSpan = Integer(options, "MaxLength", 200);
                return new("Ast", new[] { "Ast" }, bars =>
                {
                    var source = RegressionEndpoints(Closes(bars), Math.Max(1, Math.Abs(slowSpan - fastSpan)));
                    var efficiency = EfficiencyRatios(bars, Integer(options, "Length", 50));
                    return Outputs(("Ast", source.Select((v, i) =>
                    {
                        var fast = Window(source, i, fastSpan).ToArray();
                        var slow = Window(source, i, slowSpan).ToArray();
                        var low = slow.Min() + efficiency[i] * (fast.Min() - slow.Min());
                        var high = slow.Max() + efficiency[i] * (fast.Max() - slow.Max());
                        return high == low ? 0 : Math.Max(0, Math.Min(1, (v - low) / (high - low))); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }).ToArray()));
                });
            case IndicatorName.AdaptiveRelativeStrengthIndex:
                kind = AverageKind(options, 6);
                if (kind is 1 or 2 or 3 or 6) return new("Arsi", new[] { "Arsi" }, bars => AdaptiveRsiOutputs(bars, indicator));
                if (kind == 0) return null;
                return new("Arsi", new[] { "Arsi" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var gains = Average(changes.Select(v => Math.Max(0, v)).ToArray(), length, kind);
                    var losses = Average(changes.Select(v => Math.Max(0, -v)).ToArray(), length, kind);
                    var result = new double[bars.Count];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var gain = gains[i] + losses[i] == 0 ? 1 : Math.Abs(gains[i] - losses[i]) / (gains[i] + losses[i]);
                        var previous = i == 0 ? 0 : result[i - 1];
                        result[i] = previous + gain * (bars[i].Close - previous);
                    }
                    return Outputs(("Arsi", result));
                });
            case IndicatorName.AdaptiveErgodicCandlestickOscillator:
                if (kind == 0) return null;
                var candleSmooth = Integer(options, "SmoothLength", 5);
                var candleWindow = Integer(options, "StochLength", 14);
                var candleSignal = Integer(options, "SignalLength", 9);
                return new("Eco", new[] { "Eco", "Signal" }, bars =>
                {
                    var result = new double[bars.Count];
                    var first = new double[2];
                    var second = new double[2];
                    for (var i = 0; i < result.Length; i++)
                    {
                        var window = Window(bars, i, candleWindow).ToArray();
                        var high = window.Max(b => b.High);
                        var low = window.Min(b => b.Low);
                        var position = high == low ? 0 : (bars[i].Close - low) / (high - low); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                        var gain = i < 2 * (candleWindow + candleSmooth) ? 1 : 2d / (candleSmooth + 1) * Math.Abs(2 * position - 1);
                        var input = new[] { bars[i].Close - bars[i].Open, bars[i].High - bars[i].Low };
                        for (var j = 0; j < 2; j++)
                        {
                            second[j] = (1 - gain) * second[j] + gain * (1 - gain) * first[j] + gain * gain * input[j];
                            first[j] += gain * (input[j] - first[j]);
                        }
                        result[i] = second[1] == 0 ? 0 : 100 * second[0] / second[1];
                    }
                    return Outputs(("Eco", result), ("Signal", Average(result, candleSignal, kind)));
                });
            case IndicatorName.SwingIndex:
            case IndicatorName.AccumulativeSwingIndex:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                var limitMove = Number(options, 0, "LimitMove");
                var accumulated = name == IndicatorName.AccumulativeSwingIndex;
                return new(accumulated ? "Asi" : "Si", accumulated ? new[] { "Asi", "Signal" } : new[] { "Si" }, bars =>
                {
                    // Wilder (1978): combine the high/low winner cases using max/min distances.
                    var swing = bars.Select((b, i) =>
                    {
                        if (i == 0) return 0d;
                        var previous = bars[i - 1];
                        var distances = new[] { Math.Abs(b.High - previous.Close), Math.Abs(b.Low - previous.Close) };
                        var larger = distances.Max();
                        var smaller = distances.Min();
                        var range = b.High - b.Low;
                        var divisor = (larger >= range ? larger - smaller / 2 : range) + Math.Abs(previous.Close - previous.Open) / 4;
                        var scale = limitMove > 0 ? limitMove : range;
                        var change = b.Close - previous.Close + (b.Close - b.Open) / 2 + (previous.Close - previous.Open) / 4;
                        return divisor == 0 || scale == 0 ? 0 : 50 * larger * change / (divisor * scale);
                    }).ToArray();
                    if (!accumulated) return Outputs(("Si", swing));
                    var cumulative = Cumulative(swing);
                    return Outputs(("Asi", cumulative), ("Signal", Average(cumulative, length, kind)));
                });
            case IndicatorName.AbsoluteStrengthMTFIndicator:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                var strengthSmoothing = Integer(options, "SmoothLength", 25);
                return new("Bulls", new[] { "Bulls", "Bears" }, bars =>
                {
                    // Difference of two linear averages equals the average of price increments.
                    var increments = bars.Select((b, i) => b.Close - (i == 0 ? 0 : bars[i - 1].Close)).ToArray();
                    var movement = Average(increments, length, kind);
                    return Outputs(("Bulls", Average(movement.Select(v => Math.Max(v, 0)).ToArray(), strengthSmoothing, kind)),
                        ("Bears", Average(movement.Select(v => Math.Max(-v, 0)).ToArray(), strengthSmoothing, kind)));
                });
            case IndicatorName.AbsoluteStrengthIndex:
                var strengthMeanGain = 2d / (Integer(options, "MaLength", 21) + 1d);
                var strengthSignalGain = 2d / (Integer(options, "SignalLength", 34) + 1d);
                return new("Asi", new[] { "Asi" }, bars =>
                {
                    var upward = new double[bars.Count];
                    var downward = new double[bars.Count];
                    var unchanged = new double[bars.Count];
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var previous = i == 0 ? 0 : bars[i - 1].Close;
                        var value = bars[i].Close;
                        upward[i] = value > previous && previous != 0 ? value / previous - 1 : 0;
                        downward[i] = value < previous && value != 0 ? previous / value - 1 : 0;
                        unchanged[i] = value == previous ? 1d / length : 0; // NOSONAR: S1244 - Unchanged-price mass counts exact ties only.
                    }
                    var rises = Cumulative(upward);
                    var falls = Cumulative(downward);
                    var ties = Cumulative(unchanged);
                    var probability = rises.Select((v, i) => falls[i] + ties[i] == 0 ? 1
                        : (v + ties[i]) / (v + falls[i] + 2 * ties[i])).ToArray();
                    // Evaluate the zero-seeded exponential filters as their explicit impulse sums.
                    double[] Smooth(IReadOnlyList<double> input, double gain) => input.Select((_, i) =>
                        Enumerable.Range(0, i + 1).Sum(j => gain * Math.Pow(1 - gain, i - j) * input[j])).ToArray();
                    var mean = Smooth(probability, strengthMeanGain);
                    var residual = probability.Select((v, i) => v - mean[i]).ToArray();
                    var first = Smooth(residual, strengthSignalGain);
                    var second = Smooth(first, strengthSignalGain);
                    return Outputs(("Asi", residual.Select((v, i) => strengthSignalGain == 1 ? v // NOSONAR: S1244 - Unit gain selects the unfiltered signal exactly.
                        : v - first[i] - (first[i] - second[i]) / (1 - strengthSignalGain)).ToArray()));
                });
            case IndicatorName.PsychologicalLine:
                return new("Pl", new[] { "Pl" }, bars => Outputs(("Pl", bars.Select((_, i) =>
                    (new ReferenceFraction(100) * new ReferenceFraction(Enumerable.Range(Math.Max(0, i - length + 1), Math.Min(length, i + 1))
                        .Count(j => j > 0 && bars[j].Close > bars[j - 1].Close)) / new ReferenceFraction(length)).ToDouble()).ToArray())));
            case IndicatorName.AnchoredMomentum:
                if (kind == 0) return null;
                var anchorLength = Math.Max(2, Math.Min(530, 2 * length + 1));
                return new("Amom", new[] { "Amom", "Signal" }, bars =>
                {
                    var smooth = Average(Closes(bars), Integer(options, "SmoothLength", 7), kind);
                    var line = bars.Select((_, i) =>
                    {
                        var anchor = Window(bars, i, anchorLength).Average(b => b.Close);
                        return anchor == 0 ? 0 : 100 * (smooth[i] - anchor) / anchor;
                    }).ToArray();
                    return Outputs(("Amom", line), ("Signal", line.Select((_, i) =>
                        Window(line, i, Integer(options, "SignalLength", 8)).Average()).ToArray()));
                });
            case IndicatorName.DidiIndex:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Curta", new[] { "Curta", "Media", "Longa" }, bars =>
                {
                    var fast = Average(Closes(bars), Integer(options, "ShortLength", 3), kind);
                    var medium = Average(Closes(bars), Integer(options, "MediumLength", 8), kind);
                    var slow = Average(Closes(bars), Integer(options, "LongLength", 20), kind);
                    return Outputs(("Curta", fast.Select((v, i) => medium[i] == 0 ? 0 : v / medium[i]).ToArray()),
                        ("Media", medium.Select(v => v == 0 ? 0d : 1d).ToArray()),
                        ("Longa", slow.Select((v, i) => medium[i] == 0 ? 0 : v / medium[i]).ToArray()));
                });
            case IndicatorName.DetrendedSyntheticPrice:
                var syntheticGain = length > 2 ? 2d / (length + 1) : .67;
                return new("Dsp", new[] { "Dsp" }, bars =>
                {
                    var result = new double[bars.Count];
                    double fast = 0, slow = 0;
                    for (var i = 0; i < result.Length; i++)
                    {
                        var high = Math.Max(bars[i].High, i == 0 ? 0 : bars[i - 1].High);
                        var low = Math.Min(bars[i].Low, i == 0 ? 0 : bars[i - 1].Low);
                        var midpoint = (high + low) / 2;
                        if (i == 0) fast = slow = midpoint;
                        fast += syntheticGain * (midpoint - fast);
                        slow += syntheticGain / 2 * (midpoint - slow);
                        result[i] = fast - slow;
                    }
                    return Outputs(("Dsp", result));
                });
            case IndicatorName.ChandeIntradayMomentumIndex:
                return new("Cimi", new[] { "Cimi" }, bars => GainLossOutputs(bars, indicator));
            case IndicatorName.EhlersCenterofGravityOscillator:
                return new("Ecog", new[] { "Ecog" }, bars => Outputs(("Ecog", bars.Select((_, i) =>
                {
                    var count = Math.Min(length, i + 1);
                    var mass = Enumerable.Range(0, count).Sum(j => bars[i - j].Close);
                    // Centered first moment of the price masses; absent history has zero mass.
                    return mass == 0 ? 0 : Enumerable.Range(0, count)
                        .Sum(j => ((length - 1d) / 2 - j) * bars[i - j].Close) / mass;
                }).ToArray())));
            case IndicatorName.ElderImpulseSystem:
                return new("Eis", new[] { "Eis" }, bars =>
                {
                    // Discrete signs follow the documented correctly rounded EMA recurrence.
                    // The independent reference feeds back its own rounded rational predictions.
                    double[] Ema(IReadOnlyList<double> values, int period) => RoundedEma(values, period);
                    var trend = Ema(Closes(bars), length);
                    var fast = Ema(Closes(bars), Integer(options, "MacdFastLength", 12));
                    var slow = Ema(Closes(bars), Integer(options, "MacdSlowLength", 26));
                    var spread = fast.Select((v, i) => v - slow[i]).ToArray();
                    var signal = Ema(spread, Integer(options, "MacdSignalLength", 9));
                    var histogram = spread.Select((v, i) => v - signal[i]).ToArray();
                    return Outputs(("Eis", trend.Select((v, i) =>
                    {
                        if (i == 0) return 0d;
                        var trendDirection = Math.Sign(v - trend[i - 1]);
                        var momentumDirection = Math.Sign(histogram[i] - histogram[i - 1]);
                        return trendDirection == momentumDirection ? trendDirection : 0d;
                    }).ToArray()));
                });
            case IndicatorName.ChandeTrendScore:
                var startLag = Integer(options, "StartLength", 11);
                var endLag = Integer(options, "EndLength", length);
                return new("Cts", new[] { "Cts" }, bars => Outputs(("Cts", bars.Select((b, i) =>
                    (double)Enumerable.Range(startLag, Math.Max(0, endLag - startLag + 1))
                        .Sum(lag => b.Close >= (i < lag ? 0 : bars[i - lag].Close) ? 1 : -1)).ToArray())));
            case IndicatorName.TrendIntensityIndex:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                var fastWindow = Integer(options, "FastLength", length);
                var slowWindow = Integer(options, "SlowLength", 60);
                return new("Tii", new[] { "Tii" }, bars =>
                {
                    var mean = Average(Closes(bars), slowWindow, kind);
                    var residuals = bars.Select((b, i) => b.Close - mean[i]).ToArray();
                    return Outputs(("Tii", residuals.Select((_, i) =>
                    {
                        var window = Window(residuals, i, fastWindow).ToArray();
                        var total = window.Sum(Math.Abs);
                        return total == 0 ? 0 : 100 * window.Sum(v => Math.Max(0, v)) / total;
                    }).ToArray()));
                });
            case IndicatorName.TrendDetectionIndex:
                var shortWindow = Integer(options, "Length1", 20);
                var longWindow = Integer(options, "Length2", 40);
                return new("Tdi", new[] { "Tdi", "TdiDirection" }, bars =>
                {
                    var changes = bars.Select((b, i) => i < shortWindow ? 0 : b.Close - bars[i - shortWindow].Close).ToArray();
                    var direction = changes.Select((_, i) => Window(changes, i, shortWindow).Sum()).ToArray();
                    var line = changes.Select((_, i) => Math.Abs(direction[i])
                        - Enumerable.Range(Math.Max(0, i - longWindow + 1), Math.Max(0, i - shortWindow + 1) - Math.Max(0, i - longWindow + 1))
                            .Sum(j => Math.Abs(changes[j]))).ToArray();
                    return Outputs(("Tdi", line), ("TdiDirection", direction));
                });
            case IndicatorName.DiNapoliPercentagePriceOscillator:
                return new("Ppo", new[] { "Ppo", "Signal", "Histogram" }, bars =>
                {
                    // Fractional DiNapoli periods with zero seeds; expand the exponential kernels.
                    double[] Filter(double[] values, double period) => values.Select((_, i) =>
                        Enumerable.Range(0, i + 1).Sum(j => values[j] * (2 / (period + 1)) * Math.Pow(1 - 2 / (period + 1), i - j))).ToArray();
                    var fast = Filter(Closes(bars), 8.3896);
                    var slow = Filter(Closes(bars), 17.5185);
                    var line = fast.Select((v, i) => slow[i] == 0 ? 0 : 100 * (v / slow[i] - 1)).ToArray();
                    return Oscillator("Ppo", line, Filter(line, 9.0503));
                });
            case IndicatorName.MirroredPercentagePriceOscillator:
                if (kind == 0) return null;
                var mirrorSignalPeriod = Integer(options, "SignalLength", 9);
                return new("Ppo", new[] { "Ppo", "Signal", "Histogram", "MirrorPpo", "MirrorSignal", "MirrorHistogram" }, bars =>
                {
                    var open = Average(bars.Select(b => b.Open).ToArray(), length, kind);
                    var close = Average(Closes(bars), length, kind);
                    var line = close.Select((v, i) => open[i] == 0 ? 0 : 100 * (v / open[i] - 1)).ToArray();
                    // Swapping numerator and denominator is not simple negation for a percentage.
                    var mirror = open.Select((v, i) => close[i] == 0 ? 0 : 100 * (v / close[i] - 1)).ToArray();
                    var signal = Average(line, mirrorSignalPeriod, kind);
                    var mirrorSignal = Average(mirror, mirrorSignalPeriod, kind);
                    return Outputs(("Ppo", line), ("Signal", signal), ("Histogram", line.Select((v, i) => v - signal[i]).ToArray()),
                        ("MirrorPpo", mirror), ("MirrorSignal", mirrorSignal), ("MirrorHistogram", mirror.Select((v, i) => v - mirrorSignal[i]).ToArray()));
                });
            case IndicatorName.PercentagePriceOscillatorLeader:
                return new("Ppo", new[] { "Ppo", "Signal", "Histogram" }, bars =>
                {
                    // Linear smoothing makes EMA(x) + EMA(x-EMA(x)) equal DEMA(x).
                    var fastLeader = Average(Closes(bars), 12, 4);
                    var slowLeader = Average(Closes(bars), 26, 4);
                    var line = fastLeader.Select((v, i) => slowLeader[i] == 0 ? 0 : 100 * (v / slowLeader[i] - 1)).ToArray();
                    return Oscillator("Ppo", line, Average(line, length, 3));
                });
            case IndicatorName.TFSMboIndicator:
            case IndicatorName.TFSMboPercentagePriceOscillator:
            case IndicatorName.ErgodicMovingAverageConvergenceDivergence:
            case IndicatorName.ErgodicPercentagePriceOscillator:
                var tfs = name == IndicatorName.TFSMboIndicator || name == IndicatorName.TFSMboPercentagePriceOscillator;
                var percentage = name == IndicatorName.TFSMboPercentagePriceOscillator || name == IndicatorName.ErgodicPercentagePriceOscillator;
                kind = AverageKind(options, tfs ? 1 : 3);
                if (kind == 0) return null;
                var period1 = tfs ? Integer(options, "FastLength", 25) : Integer(options, "Length1", 32);
                var period2 = tfs ? Integer(options, "SlowLength", 200) : Integer(options, "Length2", percentage ? length : 5);
                var signalLength = tfs ? Integer(options, "SignalLength", 18) : Integer(options, "Length3", 5);
                var spreadKey = percentage ? "Ppo" : tfs ? "TfsMob" : "Macd";
                return new(spreadKey, new[] { spreadKey, "Signal", "Histogram" }, bars =>
                {
                    var first = Average(Closes(bars), period1, kind);
                    var second = Average(Closes(bars), period2, kind);
                    var line = first.Select((v, i) => percentage ? second[i] == 0 ? 0 : 100 * (v / second[i] - 1) : v - second[i]).ToArray();
                    return Oscillator(spreadKey, line, Average(line, signalLength, kind));
                });
            case IndicatorName.MayerMultiple:
            case IndicatorName.PrettyGoodOscillator:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                var averageRatioKey = name == IndicatorName.MayerMultiple ? "Mm" : "Pgo";
                return new(averageRatioKey, new[] { averageRatioKey }, bars =>
                {
                    var center = Average(Closes(bars), length, kind);
                    var denominator = name == IndicatorName.MayerMultiple ? center : Average(TrueRanges(bars), length, kind);
                    return Outputs((averageRatioKey, bars.Select((b, i) => denominator[i] == 0 ? 0
                        : (b.Close - (name == IndicatorName.MayerMultiple ? 0 : center[i])) / denominator[i]).ToArray()));
                });
            case IndicatorName.RelativeMomentumIndex:
                kind = AverageKind(options, 6);
                if (kind is 1 or 2 or 3 or 6) return new("Rmi", new[] { "Rmi", "Signal", "Histogram" }, bars => GainLossOutputs(bars, indicator));
                if (kind == 0) return null;
                var momentumPeriod = Integer(options, "Momentum", 3);
                return new("Rmi", new[] { "Rmi", "Signal", "Histogram" }, bars =>
                {
                    var change = bars.Select((b, i) => i < momentumPeriod ? 0 : b.Close - bars[i - momentumPeriod].Close).ToArray();
                    var up = Average(change.Select(v => Math.Max(0, v)).ToArray(), length, kind);
                    var down = Average(change.Select(v => Math.Max(0, -v)).ToArray(), length, kind);
                    var line = up.Select((v, i) => down[i] == 0 ? 100 : 100 * v / (v + down[i])).ToArray();
                    return Oscillator("Rmi", line, Average(line, length, kind));
                });
            case IndicatorName.PriceMomentumOscillator:
                if (kind is 1 or 2 or 3 or 6) return new("Pmo", new[] { "Pmo", "Signal" }, bars => RocPipelineOutputs(bars, indicator));
                if (kind == 0) return null;
                var firstPeriod = Integer(options, "Length1", length);
                var secondPeriod = Integer(options, "Length2", 20);
                var pmoSignalPeriod = Integer(options, "SignalLength", 10);
                return new("Pmo", new[] { "Pmo", "Signal" }, bars =>
                {
                    var returns = bars.Select((b, i) => i == 0 || bars[i - 1].Close == 0 ? 0 : 100 * (b.Close / bars[i - 1].Close - 1)).ToArray();
                    // Zero-seeded exponential kernels with PMO's 2/period coefficients.
                    double[] Filter(double[] values, int period) => values.Select((_, i) =>
                        Enumerable.Range(0, i + 1).Sum(j => values[j] * (2d / period) * Math.Pow(1 - 2d / period, i - j))).ToArray();
                    var line = Filter(Filter(returns, firstPeriod), secondPeriod).Select(v => 10 * v).ToArray();
                    return Outputs(("Pmo", line), ("Signal", Average(line, pmoSignalPeriod, kind)));
                });
            case IndicatorName.CoppockCurve:
                kind = AverageKind(options, 2);
                if (kind is 1 or 2 or 3 or 6) return new("Cc", new[] { "Cc" }, bars => RocPipelineOutputs(bars, indicator));
                if (kind == 0) return null;
                var fastRoc = Integer(options, "FastLength", 11);
                var slowRoc = Integer(options, "SlowLength", 14);
                return new("Cc", new[] { "Cc" }, bars =>
                {
                    double Roc(int i, int lag) => i < lag || bars[i - lag].Close == 0 ? 0 : 100 * (bars[i].Close / bars[i - lag].Close - 1);
                    var sum = bars.Select((_, i) => Roc(i, fastRoc) + Roc(i, slowRoc)).ToArray();
                    return Outputs(("Cc", Average(sum, length, kind)));
                });
            case IndicatorName.TrueStrengthIndex:
                if (kind is 1 or 2 or 3 or 6) return new("Tsi", new[] { "Tsi", "Signal" }, bars => StrengthOutputs(bars, indicator));
                if (kind == 0) return null;
                var longPeriod = Integer(options, "Length1", Integer(options, "LongLength", 25));
                var shortPeriod = Integer(options, "Length2", Integer(options, "ShortLength", 13));
                var signalPeriod = Integer(options, "SignalLength", 7);
                return new("Tsi", new[] { "Tsi", "Signal" }, bars =>
                {
                    var change = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var signed = Average(Average(change, longPeriod, kind), shortPeriod, kind);
                    var absolute = Average(Average(change.Select(Math.Abs).ToArray(), longPeriod, kind), shortPeriod, kind);
                    var line = signed.Select((v, i) => absolute[i] == 0 ? 0 : Math.Max(-100, Math.Min(100, 100 * v / absolute[i]))).ToArray();
                    return Outputs(("Tsi", line), ("Signal", Average(line, signalPeriod, kind)));
                });
            case IndicatorName.RelativeVigorIndex:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Rvi", new[] { "Rvi", "Signal" }, bars =>
                {
                    var numerator = Average(VigorFir(bars.Select(b => b.Close - b.Open).ToArray()), length, kind);
                    var denominator = Average(VigorFir(bars.Select(b => b.High - b.Low).ToArray()), length, kind);
                    var line = numerator.Select((v, i) => denominator[i] == 0 ? 0 : v / denominator[i]).ToArray();
                    return Outputs(("Rvi", line), ("Signal", VigorFir(line)));
                });
            case IndicatorName.ChandeQuickStick:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Cqs", new[] { "Cqs" }, bars => Outputs(("Cqs", Average(bars.Select(b => b.Close - b.Open).ToArray(), length, kind))));
            case IndicatorName.ElderRayIndex:
                if (kind == 0) return null;
                return new("BullPower", new[] { "BullPower", "BearPower" }, bars =>
                {
                    var center = Average(Closes(bars), length, kind);
                    return Outputs(("BullPower", bars.Select((b, i) => b.High - center[i]).ToArray()),
                        ("BearPower", bars.Select((b, i) => b.Low - center[i]).ToArray()));
                });
            case IndicatorName.DisparityIndex:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Di", new[] { "Di" }, bars =>
                {
                    var center = Average(Closes(bars), length, kind);
                    return Outputs(("Di", bars.Select((b, i) => center[i] == 0 ? 0 : 100 * (b.Close / center[i] - 1)).ToArray()));
                });
            case IndicatorName.VortexIndicator:
                return new("ViPlus", new[] { "ViPlus", "ViMinus" }, bars =>
                {
                    var tr = TrueRanges(bars);
                    var plus = bars.Select((b, i) => i == 0 ? 0 : Math.Abs(b.High - bars[i - 1].Low)).ToArray();
                    var minus = bars.Select((b, i) => i == 0 ? 0 : Math.Abs(b.Low - bars[i - 1].High)).ToArray();
                    double[] Ratio(double[] movement) => movement.Select((_, i) =>
                    {
                        var denominator = Window(tr, i, length).Sum();
                        return denominator == 0 ? 0 : Window(movement, i, length).Sum() / denominator;
                    }).ToArray();
                    return Outputs(("ViPlus", Ratio(plus)), ("ViMinus", Ratio(minus)));
                });
            case IndicatorName.UltimateOscillator:
                var periods = new[] { Integer(options, "Length1", 7), Integer(options, "Length2", 14), Integer(options, "Length3", 28) };
                return new("Uo", new[] { "Uo" }, bars =>
                {
                    var tr = TrueRanges(bars);
                    var pressure = bars.Select((b, i) => b.Close - Math.Min(b.Low, i == 0 ? b.Close : bars[i - 1].Close)).ToArray();
                    return Outputs(("Uo", bars.Select((_, i) => 100d / 7 * periods.Select((period, stage) =>
                    {
                        var denominator = Window(tr, i, period).Sum();
                        return denominator == 0 ? 0 : Math.Pow(2, 2 - stage) * Window(pressure, i, period).Sum() / denominator;
                    }).Sum()).ToArray()));
                });
            case IndicatorName.AwesomeOscillator:
            case IndicatorName.AcceleratorOscillator:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                var fastLength = Integer(options, "FastLength", length);
                var slowLength = Integer(options, "SlowLength", 34);
                var smoothLength = Integer(options, "SmoothLength", 5);
                var awesomeKey = name == IndicatorName.AwesomeOscillator ? "Ao" : "Ac";
                return new(awesomeKey, new[] { awesomeKey }, bars =>
                {
                    var median = bars.Select(b => (b.High + b.Low) / 2).ToArray();
                    var fastAverage = Average(median, fastLength, kind);
                    var slowAverage = Average(median, slowLength, kind);
                    var difference = fastAverage.Select((v, i) => v - slowAverage[i]).ToArray();
                    if (name == IndicatorName.AwesomeOscillator) return Outputs((awesomeKey, difference));
                    var smoothed = Average(difference, smoothLength, kind);
                    return Outputs((awesomeKey, difference.Select((v, i) => v - smoothed[i]).ToArray()));
                });
            case IndicatorName.AroonUp:
            case IndicatorName.AroonDown:
            case IndicatorName.AroonOscillator:
                var oscillator = name == IndicatorName.AroonOscillator;
                var key = oscillator ? "Aroon" : name == IndicatorName.AroonUp ? "AroonUp" : "AroonDown";
                return new(key, oscillator ? new[] { "Aroon", "AroonUp", "AroonDown" } : new[] { key }, bars =>
                {
                    // The published oscillator uses closes and a length-bar window.
                    // Standalone legs use OHLC extremes and length prior bars plus the current bar.
                    var up = Leg(true);
                    var down = Leg(false);
                    return Outputs(("AroonUp", up), ("AroonDown", down), ("Aroon", up.Select((v, i) => v - down[i]).ToArray()));
                    double[] Leg(bool high) => bars.Select((_, i) =>
                    {
                        if (!oscillator && i < length) return 0;
                        var first = Math.Max(0, i - (oscillator ? Math.Max(2, length) - 1 : length));
                        var candidates = Enumerable.Range(first, i - first + 1)
                            .Select(j => (Index: j, Price: oscillator ? bars[j].Close : high ? bars[j].High : bars[j].Low));
                        var chosen = high ? candidates.OrderByDescending(v => v.Price).ThenByDescending(v => v.Index).First()
                            : candidates.OrderBy(v => v.Price).ThenByDescending(v => v.Index).First();
                        return 100d * (length - i + chosen.Index) / length;
                    }).ToArray();
                });
            case IndicatorName.BalanceOfPower:
                if (kind == 0) return null;
                return new("Bop", new[] { "Bop", "BopSignal" }, bars =>
                {
                    var line = bars.Select(b => b.High == b.Low ? 0 : (b.Close - b.Open) / (b.High - b.Low)).ToArray(); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    return Outputs(("Bop", line), ("BopSignal", Average(line, length, kind)));
                });
            case IndicatorName.Trix:
                if (kind == 0) return null;
                return new("Trix", new[] { "Trix" }, bars =>
                {
                    var triple = Average(Average(Average(Closes(bars), length, kind), length, kind), length, kind);
                    return Outputs(("Trix", triple.Select((v, i) => i == 0 || triple[i - 1] == 0 ? 0
                        : 100 * (v - triple[i - 1]) / Math.Abs(triple[i - 1])).ToArray()));
                });
            case IndicatorName.DetrendedPriceOscillator:
                kind = AverageKind(options, 1);
                if (kind == 0) return null;
                return new("Dpo", new[] { "Dpo" }, bars =>
                {
                    var average = Average(Closes(bars), length, kind);
                    var offset = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d + 1)));
                    return Outputs(("Dpo", average.Select((v, i) => (i >= offset ? bars[i - offset].Close : 0) - v).ToArray()));
                });
            case IndicatorName.MassIndex:
                if (kind == 0) return null;
                var firstLength = Integer(options, "EmaLength", 21);
                var sumLength = Integer(options, "SumLength", length);
                return new("Mi", new[] { "Mi", "Signal" }, bars =>
                {
                    var first = Average(bars.Select(b => b.High - b.Low).ToArray(), firstLength, kind);
                    var second = Average(first, firstLength, kind);
                    var ratio = first.Select((v, i) => second[i] == 0 ? 0 : v / second[i]).ToArray();
                    var line = ratio.Select((_, i) => Window(ratio, i, sumLength).Sum()).ToArray();
                    return Outputs(("Mi", line), ("Signal", Average(line, 9, kind)));
                });
            default: return null;
        }
    }

    private static double[] VigorFir(IReadOnlyList<double> values)
    {
        // Convolving a two-point mean with a three-point mean gives weights 1,2,2,1 / 6.
        var two = values.Select((v, i) => (v + (i == 0 ? 0 : values[i - 1])) / 2).ToArray();
        return two.Select((v, i) => (v + (i >= 1 ? two[i - 1] : 0) + (i >= 2 ? two[i - 2] : 0)) / 3).ToArray();
    }
}
