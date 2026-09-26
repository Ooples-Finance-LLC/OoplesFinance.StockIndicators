using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedLinda(IReadOnlyList<Bar> bars, object options)
    {
        var kind = BoundedMeanKind(options, 1);
        var fast = RoundedBoundedStage(Closes(bars), Integer(options, "FastLength", 3), kind);
        var slow = RoundedBoundedStage(Closes(bars), Integer(options, "SlowLength", 10), kind);
        double[] Line(double[] numerator, double[] denominator, bool percentage) => numerator.Select((value, i) =>
            percentage ? denominator[i] == 0 ? 0 : (new ReferenceFraction(100) *
                (ReferenceFraction.FromDouble(value) / ReferenceFraction.FromDouble(denominator[i]) - new ReferenceFraction(1))).ToDouble()
            : (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(denominator[i])).ToDouble()).ToArray();
        (double[] Signal, double[] Histogram) Smooth(double[] line)
        {
            var count = Array.FindIndex(line, double.IsInfinity);
            if (count < 0) count = line.Length;
            var signal = RoundedBoundedStage(line.Take(count).ToArray(), Integer(options, "SmoothLength", 16), kind)
                .Concat(Enumerable.Repeat(double.NaN, line.Length - count)).ToArray();
            var histogram = line.Select((value, i) => i >= count ? double.NaN :
                (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(signal[i])).ToDouble()).ToArray();
            return (signal, histogram);
        }
        var macd = Line(fast, slow, false);
        var ppo = Line(fast, slow, true);
        var difference = Smooth(macd);
        var ratioOutputs = Smooth(ppo);
        return Outputs(("LindaMacd", macd), ("LindaMacdSignal", difference.Signal), ("LindaMacdHistogram", difference.Histogram),
            ("LindaPpo", ppo), ("LindaPpoSignal", ratioOutputs.Signal), ("LindaPpoHistogram", ratioOutputs.Histogram));
    }
}
