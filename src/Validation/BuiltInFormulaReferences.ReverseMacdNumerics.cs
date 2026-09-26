using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedReverseMacdReference(IReadOnlyList<Bar> bars, object options)
    {
        var fastLength = Integer(options, "FastLength", 12);
        var slowLength = Integer(options, "SlowLength", 26);
        var fast = RoundedBoundedStage(Closes(bars), fastLength, 3);
        var slow = RoundedBoundedStage(Closes(bars), slowLength, 3);
        var a = ReferenceFraction.FromDouble(2d / (1d + fastLength));
        var b = ReferenceFraction.FromDouble(2d / (1d + slowLength));
        var line = new double[bars.Count];
        for (var i = 1; i < line.Length; i++)
        {
            var previous = ReferenceFraction.FromDouble(fast[i - 1]);
            // The price correction exactly offsets the unequal EMA changes.
            line[i] = fastLength == slowLength ? fast[i - 1] :
                (previous + b * (previous - ReferenceFraction.FromDouble(slow[i - 1])) / (a - b)).ToDouble();
        }
        var count = line.TakeWhile(v => !double.IsNaN(v) && !double.IsInfinity(v)).Count();
        var signal = RoundedBoundedStage(line.Take(count).ToArray(), 9, 3)
            .Concat(Enumerable.Repeat(double.NaN, line.Length - count)).ToArray();
        return Outputs(("Rmacd", line), ("Signal", signal), ("Histogram", line.Select((v, i) => v - signal[i]).ToArray()));
    }
}
