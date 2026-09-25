using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedFourOscillator(IReadOnlyList<Bar> bars, object options, bool percentage)
    {
        var kind = BoundedMeanKind(options, 3);
        var input = Closes(bars);
        var first = RoundedBoundedStage(input, Integer(options, "Length1", 5), kind);
        var second = RoundedBoundedStage(input, Integer(options, "Length3", 10), kind);
        var third = RoundedBoundedStage(input, Integer(options, "Length4", 17), kind);
        var fourth = RoundedBoundedStage(input, Integer(options, "Length2", 8), kind);
        double[] Line(double[] numerator, double[] denominator) => numerator.Select((value, i) =>
            percentage ? denominator[i] == 0 ? 0 : (new ReferenceFraction(100) *
                (ReferenceFraction.FromDouble(value) / ReferenceFraction.FromDouble(denominator[i]) - new ReferenceFraction(1))).ToDouble()
            : (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(denominator[i])).ToDouble()).ToArray();
        (double[] Signal, double[] Histogram) Smooth(double[] line)
        {
            var count = Array.FindIndex(line, double.IsInfinity);
            if (count < 0) count = line.Length;
            var signal = RoundedBoundedStage(line.Take(count).ToArray(), Integer(options, "Length1", 5), kind)
                .Concat(Enumerable.Repeat(double.NaN, line.Length - count)).ToArray();
            var histogram = line.Select((value, i) => i >= count ? double.NaN :
                (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(signal[i])).ToDouble()).ToArray();
            return (signal, histogram);
        }
        // The published first pair is A(length1)-A(length3), the second A(length4)-A(length2).
        // Length5/6 and the blue/yellow multipliers belong only to unpublished auxiliary lines.
        var one = Line(first, second);
        var two = Line(third, fourth);
        var firstOutputs = Smooth(one);
        var secondOutputs = Smooth(two);
        var stem = percentage ? "Ppo" : "Macd";
        return Outputs((stem + "1", one), ("Signal1", firstOutputs.Signal), ("Histogram1", firstOutputs.Histogram),
            (stem + "2", two), ("Signal2", secondOutputs.Signal), ("Histogram2", secondOutputs.Histogram));
    }
}
