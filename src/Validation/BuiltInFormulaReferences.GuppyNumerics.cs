using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedGuppyDistance(IReadOnlyList<Bar> bars, object options)
    {
        var kind = BoundedMeanKind(options, 3);
        double[] Distance(int first)
        {
            var ribbon = Enumerable.Range(first, 6).Select(j =>
                RoundedBoundedStage(Closes(bars), Integer(options, "Length" + j, 1), kind)).ToArray();
            return bars.Select((_, i) => Enumerable.Range(1, 5).Aggregate(new ReferenceFraction(0),
                (sum, j) => sum + (ReferenceFraction.FromDouble(ribbon[j][i]) -
                    ReferenceFraction.FromDouble(ribbon[j - 1][i])).Abs()).ToDouble()).ToArray();
        }
        return Outputs(("FastDistance", Distance(1)), ("SlowDistance", Distance(7)));
    }

    internal static IReadOnlyDictionary<string, double[]> RoundedGuppy(IReadOnlyList<Bar> bars, object options)
    {
        var kind = BoundedMeanKind(options, 3);
        var fastPeriods = new[] { 1, 2, 3, 5, 7, 9, 10, 11, 12, 13, 14 }.Select(j => Integer(options, "Length" + j, 1));
        var slowPeriods = new[] { 15, 16, 18, 19, 21, 22, 23, 25, 26 }.Select(j => Integer(options, "Length" + j, 1))
            .Concat(new[] { 52, 55, 58, 61, 64, 67, 70 });
        var fast = fastPeriods.Select(n => RoundedBoundedStage(Closes(bars), n, kind)).ToArray();
        var slow = slowPeriods.Select(n => RoundedBoundedStage(Closes(bars), n, kind)).ToArray();
        ReferenceFraction Mean(double[][] values, int i) => ReferenceFraction.FromDouble(
            (values.Aggregate(new ReferenceFraction(0), (sum, row) => sum + ReferenceFraction.FromDouble(row[i])) /
                new ReferenceFraction(values.Length)).ToDouble());
        var line = bars.Select((_, i) =>
        {
            var denominator = Mean(slow, i);
            return denominator.Sign == 0 ? 0 : ((Mean(fast, i) - denominator) * new ReferenceFraction(100) / denominator).ToDouble();
        }).ToArray();
        var count = Array.FindIndex(line, double.IsInfinity);
        if (count < 0) count = line.Length;
        var signal = RoundedBoundedStage(line.Take(count).ToArray(), 13, kind)
            .Concat(Enumerable.Repeat(double.NaN, line.Length - count)).ToArray();
        return Outputs(("SuperGmmaOsc", line), ("SuperGmmaSignal", signal));
    }
}
