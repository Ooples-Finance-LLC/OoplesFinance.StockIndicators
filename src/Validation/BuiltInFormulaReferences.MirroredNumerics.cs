using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedMirrored(IReadOnlyList<Bar> bars, object options, bool percentage)
    {
        var kind = BoundedMeanKind(options, 3);
        var length = Integer(options, "Length", 20);
        var open = RoundedBoundedStage(bars.Select(b => b.Open).ToArray(), length, kind);
        var close = RoundedBoundedStage(Closes(bars), length, kind);
        double[] Line(double[] numerator, double[] denominator) => numerator.Select((value, i) =>
            percentage ? denominator[i] == 0 ? 0 : (new ReferenceFraction(100) *
                (ReferenceFraction.FromDouble(value) / ReferenceFraction.FromDouble(denominator[i]) - new ReferenceFraction(1))).ToDouble()
            : (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(denominator[i])).ToDouble()).ToArray();
        (double[] Signal, double[] Histogram) Smooth(double[] line)
        {
            var count = Array.FindIndex(line, double.IsInfinity);
            if (count < 0) count = line.Length;
            var signal = RoundedBoundedStage(line.Take(count).ToArray(), Integer(options, "SignalLength", 9), kind)
                .Concat(Enumerable.Repeat(double.NaN, line.Length - count)).ToArray();
            var histogram = line.Select((value, i) => i >= count ? double.NaN :
                (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(signal[i])).ToDouble()).ToArray();
            return (signal, histogram);
        }
        // Each mean is published/rounded before subtraction; percentages have different denominators.
        var line = Line(close, open);
        var mirror = Line(open, close);
        var normal = Smooth(line);
        var reflected = Smooth(mirror);
        var stem = percentage ? "Ppo" : "Macd";
        return Outputs((stem, line), ("Signal", normal.Signal), ("Histogram", normal.Histogram),
            ("Mirror" + stem, mirror), ("MirrorSignal", reflected.Signal), ("MirrorHistogram", reflected.Histogram));
    }
}
