using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private sealed class FormulaDefinition
    {
        internal FormulaDefinition(string primary, string[] keys,
            Func<IReadOnlyList<Bar>, IReadOnlyDictionary<string, double[]>> compute)
        { Primary = primary; Keys = keys; Compute = compute; }
        internal string Primary { get; }
        internal string[] Keys { get; }
        internal Func<IReadOnlyList<Bar>, IReadOnlyDictionary<string, double[]>> Compute { get; }
    }

    // Equal built-in average types collapse to the configured enum at each smoothing stage.
    // A custom average or heterogeneous stages cannot be described by that single enum.
    private static bool UniformBuiltInComponents(IIndicator indicator) => indicator.Components.Count == 0
        || indicator.Components.Count == 1 && indicator.Components.All(c => c is IBuiltInMovingAverage)
        && indicator.Components.Cast<IBuiltInMovingAverage>().Select(c => c.AvgType).Distinct().Count() == 1;

    private static int AverageKind(object options, int fallback)
    {
        var value = options.GetType().GetProperty("MaType")?.GetValue(options)
            ?? options.GetType().GetProperty("MovingAvgType")?.GetValue(options);
        return value is MovingAvgType average ? average switch
        {
            MovingAvgType.SimpleMovingAverage => 1,
            MovingAvgType.WeightedMovingAverage => 2,
            MovingAvgType.ExponentialMovingAverage => 3,
            MovingAvgType.DoubleExponentialMovingAverage => 4,
            MovingAvgType.TripleExponentialMovingAverage => 5,
            MovingAvgType.WildersSmoothingMethod => 6,
            _ => 0
        } : fallback;
    }

    private static double Number(object options, double fallback, params string[] names)
    {
        foreach (var name in names)
            if (options.GetType().GetProperty(name)?.GetValue(options) is double value) return value;
        return fallback;
    }

    private static FormulaDefinition? Foundation(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        // Options are metadata only. All calculations below use independent direct arithmetic.
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 14);
        var defaultAverage = name switch
        {
            IndicatorName.RelativeStrengthIndex or IndicatorName.AverageTrueRange or IndicatorName.AverageDirectionalIndex => 6,
            IndicatorName.HullMovingAverage or IndicatorName.MomentumOscillator => 2,
            IndicatorName.OnBalanceVolume or IndicatorName.AccumulationDistributionLine
                or IndicatorName.AbsolutePriceOscillator or IndicatorName.PercentageVolumeOscillator
                or IndicatorName.ZeroLagExponentialMovingAverage => 3,
            _ => 1
        };
        var kind = AverageKind(options, defaultAverage);
        if (kind == 0 || kind is >= 3 and <= 5 && !StandardEmaPeriod(length)) return null;
        var signalLength = Integer(options, "SignalLength", name == IndicatorName.RelativeStrengthIndex ? 3 : 9);
        double[] Smooth(IReadOnlyList<double> values, int? period = null) => Average(values, period ?? length, kind);
        FormulaDefinition Single(string key, Func<IReadOnlyList<Bar>, double[]> formula) =>
            new(key, new[] { key }, bars => Outputs((key, formula(bars))));

        switch (name)
        {
            case IndicatorName.WellesWilderMovingAverage:
                return Single("Wwma", bars => Average(Closes(bars), length, 6));
            case IndicatorName.TriangularMovingAverage:
                return Single("Tma", bars => Smooth(Smooth(Closes(bars))));
            case IndicatorName.ZeroLagExponentialMovingAverage:
                return Single("Zema", bars =>
                {
                    var first = Smooth(Closes(bars));
                    var second = Smooth(first);
                    return first.Select((v, i) => 2 * v - second[i]).ToArray();
                });
            case IndicatorName.HullMovingAverage:
                return Single("Hma", bars =>
                {
                    var prices = Closes(bars);
                    var half = Smooth(prices, Math.Max(1, (int)Math.Round(length / 2d)));
                    var full = Smooth(prices);
                    return Smooth(half.Select((v, i) => 2 * v - full[i]).ToArray(), Math.Max(1, (int)Math.Round(Math.Sqrt(length))));
                });
            case IndicatorName.VolumeWeightedMovingAverage:
                if (kind == 1) return Single("Vwma", bars => RoundedRollingVolumeMean(bars, length));
                return Single("Vwma", bars =>
                {
                    var denominator = Smooth(bars.Select(b => (double)b.Volume).ToArray());
                    var products = bars.Select(b => b.Close * (double)b.Volume).ToArray();
                    return products.Select((_, i) => denominator[i] == 0 ? 0
                        : Window(products, i, length).Average() / denominator[i]).ToArray();
                });
            case IndicatorName.RateOfChange:
                return Single("Roc", bars => bars.Select((b, i) => i < length || bars[i - length].Close == 0
                    ? 0 : 100 * (b.Close / bars[i - length].Close - 1)).ToArray());
            case IndicatorName.MomentumOscillator:
                return new("Mo", new[] { "Mo", "Signal" }, bars =>
                {
                    var line = bars.Select((b, i) => i < length || bars[i - length].Close == 0
                        ? 0 : 100 * b.Close / bars[i - length].Close).ToArray();
                    return Outputs(("Mo", line), ("Signal", Smooth(line)));
                });
            case IndicatorName.RelativeStrengthIndex:
                return new("Rsi", new[] { "Rsi", "Signal", "Histogram" }, bars =>
                {
                    var changes = bars.Select((b, i) => i == 0 ? 0 : b.Close - bars[i - 1].Close).ToArray();
                    var gains = Smooth(changes.Select(v => Math.Max(0, v)).ToArray());
                    var losses = Smooth(changes.Select(v => Math.Max(0, -v)).ToArray());
                    var rsi = gains.Select((v, i) => losses[i] == 0 ? 100 : 100 * v / (v + losses[i])).ToArray();
                    return Oscillator("Rsi", rsi, Smooth(rsi, signalLength));
                });
            case IndicatorName.AverageDirectionalIndex:
                return new("Adx", new[] { "DiPlus", "DiMinus", "Adx" }, bars =>
                {
                    var positive = bars.Select((b, i) => i == 0 ? 0 :
                        b.High - bars[i - 1].High > bars[i - 1].Low - b.Low ? Math.Max(0, b.High - bars[i - 1].High) : 0).ToArray();
                    var negative = bars.Select((b, i) => i == 0 ? 0 :
                        bars[i - 1].Low - b.Low > b.High - bars[i - 1].High ? Math.Max(0, bars[i - 1].Low - b.Low) : 0).ToArray();
                    var tr = Smooth(TrueRanges(bars));
                    var plus = Smooth(positive).Select((v, i) => tr[i] == 0 ? 0 : 100 * v / tr[i]).ToArray();
                    var minus = Smooth(negative).Select((v, i) => tr[i] == 0 ? 0 : 100 * v / tr[i]).ToArray();
                    var dx = plus.Select((v, i) => v + minus[i] == 0 ? 0 : 100 * Math.Abs(v - minus[i]) / (v + minus[i])).ToArray();
                    return Outputs(("DiPlus", plus), ("DiMinus", minus), ("Adx", Smooth(dx)));
                });
            case IndicatorName.AverageTrueRange:
                return Single("Atr", bars => Smooth(TrueRanges(bars)));
            case IndicatorName.NormalizedAverageTrueRange:
                return Single("Natr", bars =>
                {
                    var atr = Average(TrueRanges(bars), length, 6);
                    return bars.Select((b, i) => b.Close == 0 ? 0 : 100 * atr[i] / b.Close).ToArray();
                });
            case IndicatorName.StandardDeviationVolatility:
                return new("StdDev", new[] { "StdDev", "Variance", "Signal" }, bars =>
                {
                    var prices = Closes(bars);
                    var mean = Smooth(prices);
                    var variance = Smooth(prices.Select((v, i) => (v - mean[i]) * (v - mean[i])).ToArray());
                    var deviation = variance.Select(Math.Sqrt).ToArray();
                    return Outputs(("StdDev", deviation), ("Variance", variance), ("Signal", Smooth(deviation)));
                });
            case IndicatorName.StandardDeviation:
                return new("Std", new[] { "Std", "Signal" }, bars =>
                {
                    var deviation = PopulationDeviation(Closes(bars), length);
                    var signal = kind == 3 ? RoundedEma(deviation, length) : kind is 1 or 2
                        ? ExactDeviationSignal(deviation, length, kind) : Smooth(deviation);
                    return Outputs(("Std", deviation), ("Signal", signal));
                });
            case IndicatorName.TrueRange:
                return Single("Tr", TrueRanges);
            case IndicatorName.Variance:
                return Single("Variance", bars => PopulationVariance(Closes(bars), length));
            case IndicatorName.DonchianChannels:
                return new("MiddleChannel", new[] { "UpperChannel", "LowerChannel", "MiddleChannel" }, bars =>
                {
                    var high = bars.Select((_, i) => Window(bars, i, length).Max(b => b.High)).ToArray();
                    var low = bars.Select((_, i) => Window(bars, i, length).Min(b => b.Low)).ToArray();
                    return Outputs(("UpperChannel", high), ("LowerChannel", low),
                        ("MiddleChannel", high.Select((v, i) => ExactPriceMean(v, low[i])).ToArray()));
                });
            case IndicatorName.BollingerBands:
            case IndicatorName.BollingerBandsPercentB:
            case IndicatorName.BollingerBandsWidth:
                var multiplier = Number(options, 2, "StdDevMult", "Multiplier", "StdDev");
                var keys = name == IndicatorName.BollingerBands ? new[] { "UpperBand", "MiddleBand", "LowerBand" }
                    : new[] { name == IndicatorName.BollingerBandsPercentB ? "PctB" : "BbWidth" };
                return new(name == IndicatorName.BollingerBands ? "MiddleBand" : keys[0], keys, bars =>
                {
                    var prices = Closes(bars);
                    var middle = Smooth(prices);
                    var width = PopulationVariance(prices, length).Select(v => multiplier * Math.Sqrt(v)).ToArray();
                    var upper = middle.Select((v, i) => v + width[i]).ToArray();
                    var lower = middle.Select((v, i) => v - width[i]).ToArray();
                    return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower),
                        ("PctB", prices.Select((v, i) => width[i] == 0 ? 0 : 100 * (v - lower[i]) / (2 * width[i])).ToArray()),
                        ("BbWidth", middle.Select((v, i) => v == 0 ? 0 : 2 * width[i] / v).ToArray()));
                });
            case IndicatorName.AbsolutePriceOscillator:
            case IndicatorName.ElliottWaveOscillator:
            case IndicatorName.PercentageVolumeOscillator:
                var fast = options is PriceOscillatorSpecOptions priceOptions ? priceOptions.ShortLength
                    : options is PvoSpecOptions pvo ? pvo.Length : Integer(options, "FastLength");
                var slow = options is PriceOscillatorSpecOptions slowOptions ? slowOptions.LongLength
                    : options is PvoSpecOptions ? 26 : Integer(options, "SlowLength");
                if (kind is >= 3 and <= 5 && (!StandardEmaPeriod(fast) || !StandardEmaPeriod(slow)
                    || !StandardEmaPeriod(signalLength))) return null;
                var primary = name == IndicatorName.AbsolutePriceOscillator ? "Apo"
                    : name == IndicatorName.ElliottWaveOscillator ? "Ewo" : "Pvo";
                return new(primary, name == IndicatorName.AbsolutePriceOscillator ? new[] { primary }
                    : new[] { primary, "Signal", "Histogram" }, bars =>
                {
                    var input = name == IndicatorName.PercentageVolumeOscillator
                        ? bars.Select(b => (double)b.Volume).ToArray() : Closes(bars);
                    var first = Smooth(input, fast);
                    var second = Smooth(input, slow);
                    var line = first.Select((v, i) => name == IndicatorName.PercentageVolumeOscillator
                        ? second[i] == 0 ? 0 : 100 * (v / second[i] - 1) : v - second[i]).ToArray();
                    return Oscillator(primary, line, Smooth(line, name == IndicatorName.ElliottWaveOscillator ? fast : signalLength));
                });
            case IndicatorName.OnBalanceVolume:
            case IndicatorName.AccumulationDistributionLine:
                var volumeKey = name == IndicatorName.OnBalanceVolume ? "Obv" : "Adl";
                return new(volumeKey, new[] { volumeKey, volumeKey + "Signal" }, bars =>
                {
                    var increments = bars.Select((b, i) => name == IndicatorName.OnBalanceVolume
                        ? Math.Sign(b.Close - (i == 0 ? 0 : bars[i - 1].Close)) * (double)b.Volume
                        : b.High == b.Low ? 0 : (2 * b.Close - b.High - b.Low) / (b.High - b.Low) * (double)b.Volume).ToArray(); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    var line = Cumulative(increments);
                    return Outputs((volumeKey, line), (volumeKey + "Signal", Smooth(line)));
                });
            case IndicatorName.MoneyFlowIndex:
                return Single("Mfi", bars =>
                {
                    var typical = bars.Select(b => (b.High + b.Low + b.Close) / 3).ToArray();
                    var positive = typical.Select((v, i) => i > 0 && v > typical[i - 1] ? v * (double)bars[i].Volume : 0).ToArray();
                    var negative = typical.Select((v, i) => i > 0 && v < typical[i - 1] ? v * (double)bars[i].Volume : 0).ToArray();
                    return typical.Select((_, i) =>
                    {
                        var pos = Window(positive, i, length).Sum();
                        var neg = Window(negative, i, length).Sum();
                        return neg == 0 ? 100 : 100 * pos / (pos + neg);
                    }).ToArray();
                });
            default: return null;
        }
    }

    private static double[] Closes(IReadOnlyList<Bar> bars) => bars.Select(b => b.Close).ToArray();
    private static IEnumerable<T> Window<T>(IReadOnlyList<T> values, int end, int length)
    {
        for (var i = Math.Max(0, end - length + 1); i <= end; i++) yield return values[i];
    }
    internal static double[] PopulationDeviation(IReadOnlyList<double> prices, int length) => prices.Select((_, i) =>
    {
        if (i + 1 < length) return 0;
        var values = Window(prices, i, length).Select(ReferenceFraction.FromDouble).ToArray();
        var mean = values.Aggregate(new ReferenceFraction(0), (sum, x) => sum + x) / new ReferenceFraction(length);
        var variance = values.Aggregate(new ReferenceFraction(0), (sum, x) => sum + (x - mean) * (x - mean)) / new ReferenceFraction(length);
        return variance.SqrtToDouble();
    }).ToArray();

    private static double[] ExactDeviationSignal(IReadOnlyList<double> values, int length, int kind) => values.Select((_, i) =>
    {
        if (kind == 1 && i + 1 < length) return 0;
        var sum = new ReferenceFraction(0);
        for (var j = Math.Max(0, i - length + 1); j <= i; j++)
            sum += ReferenceFraction.FromDouble(values[j]) * new ReferenceFraction(kind == 2 ? length - i + j : 1);
        return (sum / new ReferenceFraction(kind == 2 ? (long)length * (length + 1L) / 2 : length)).ToDouble();
    }).ToArray();

    private static double[] PopulationVariance(IReadOnlyList<double> prices, int length) => prices.Select((_, i) =>
    {
        if (i + 1 < length) return 0;
        var window = Window(prices, i, length).ToArray();
        return CenteredVariance(window, false);
    }).ToArray();
    private static double[] TrueRanges(IReadOnlyList<Bar> bars) => bars.Select((b, i) =>
        i == 0 ? b.High - b.Low : Math.Max(b.High, bars[i - 1].Close) - Math.Min(b.Low, bars[i - 1].Close)).ToArray();
    private static double[] Cumulative(IReadOnlyList<double> values)
    {
        var result = new double[values.Count];
        double sum = 0;
        for (var i = 0; i < values.Count; i++) result[i] = sum += values[i];
        return result;
    }
    private static IReadOnlyDictionary<string, double[]> Outputs(params (string Key, double[] Values)[] outputs) =>
        outputs.ToDictionary(o => o.Key, o => o.Values, StringComparer.Ordinal);
    private static IReadOnlyDictionary<string, double[]> Oscillator(string key, double[] line, double[] signal) =>
        Outputs((key, line), ("Signal", signal), ("Histogram", line.Select((v, i) => v - signal[i]).ToArray()));
}
