using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    // Stages retain binary64 precision with one extra upper exponent bit. The
    // rational oracle rebuilds chronological windows rather than updating sums.
    private static ReferenceFraction RoundStrengthStage(ReferenceFraction value)
    {
        var rounded = value.ToDouble();
        return double.IsInfinity(rounded)
            ? ReferenceFraction.FromDouble((value / new ReferenceFraction(2)).ToDouble()) * new ReferenceFraction(2)
            : ReferenceFraction.FromDouble(rounded);
    }

    internal static IReadOnlyDictionary<string, double[]> StrengthOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var kind = AverageKind(options, 3);
        var twoStrengths = indicator.BatchName == IndicatorName.ErgodicTrueStrengthIndexV2;
        var ergodic = twoStrengths || indicator.BatchName == IndicatorName.ErgodicTrueStrengthIndexV1;
        var lengths = ergodic
            ? new[] { Integer(options, "Length1", 4), Integer(options, "Length2", 8), Integer(options, "Length3", 6) }
            : new[] { Integer(options, "Length1", Integer(options, "LongLength", 25)), Integer(options, "Length2", Integer(options, "ShortLength", 13)) };
        var signed = bars.Select((bar, i) => i == 0 ? new ReferenceFraction(0) :
            RoundStrengthStage(ReferenceFraction.FromDouble(bar.Close) - ReferenceFraction.FromDouble(bars[i - 1].Close))).ToArray();
        var absolute = signed.Select(v => v.Sign < 0 ? new ReferenceFraction(0) - v : v).ToArray();
        ReferenceFraction[] Smooth(ReferenceFraction[] input, int length)
        {
            var result = new ReferenceFraction[input.Length];
            for (var i = 0; i < input.Length; i++)
            {
                var total = new ReferenceFraction(0);
                if (kind == 1 && i + 1 < length) { result[i] = total; continue; }
                if (kind == 6 || kind == 3 && i >= length)
                {
                    var previous = i == 0 ? total : result[i - 1];
                    total = (previous * new ReferenceFraction(length - 1L) + input[i] * new ReferenceFraction(kind == 3 ? 2 : 1))
                        / new ReferenceFraction(kind == 3 ? length + 1L : length);
                }
                else
                {
                    for (var j = Math.Max(0, i - length + 1); j <= i; j++)
                        total += input[j] * new ReferenceFraction(kind == 2 ? length - (i - j) : 1);
                    total /= kind == 2 ? new ReferenceFraction(length) * new ReferenceFraction(length + 1L) / new ReferenceFraction(2)
                        : new ReferenceFraction(kind == 3 ? Math.Min(i + 1, length) : length);
                }
                result[i] = RoundStrengthStage(total);
            }
            return result;
        }
        double[] Strength(int[] periods)
        {
            var numerator = signed; var denominator = absolute;
            foreach (var period in periods) { numerator = Smooth(numerator, period); denominator = Smooth(denominator, period); }
            return numerator.Select((v, i) => denominator[i].Sign == 0 ? 0 :
                Math.Max(-100, Math.Min(100, (v * new ReferenceFraction(100) / denominator[i]).ToDouble()))).ToArray();
        }
        var line = Strength(lengths);
        if (!twoStrengths) return Outputs((ergodic ? "Etsi" : "Tsi", line),
            ("Signal", Average(line, Integer(options, "SignalLength", ergodic ? 3 : 7), kind)));
        var second = Strength(new[] { Integer(options, "Length4", 17), Integer(options, "Length5", 6), Integer(options, "Length6", 2) });
        return Outputs(("Etsi1", line), ("Etsi2", second), ("Signal", Average(second, Integer(options, "SignalLength", 2), kind)));
    }
}
