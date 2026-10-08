using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedStochasticMacdReference(IReadOnlyList<Bar> bars, object options)
    {
        var fast = RoundedBoundedStage(Closes(bars), 12, 3);
        var slow = RoundedBoundedStage(Closes(bars), 26, 3);
        var length = Integer(options, "Length", 45);
        var line = bars.Select((_, i) =>
        {
            var window = Window(bars, i, length).ToArray();
            var high = window.Max(b => b.High);
            var low = window.Min(b => b.Low);
#pragma warning disable S1244 // Only an exactly zero range uses the degenerate result; nonzero subnormal ranges remain meaningful.
            return high == low ? 0 : (new ReferenceFraction(10) *
#pragma warning restore S1244
                (ReferenceFraction.FromDouble(fast[i]) - ReferenceFraction.FromDouble(slow[i])) /
                (ReferenceFraction.FromDouble(high) - ReferenceFraction.FromDouble(low))).ToDouble();
        }).ToArray();
        var count = line.TakeWhile(v => !double.IsInfinity(v)).Count();
        var signal = RoundedBoundedStage(line.Take(count).ToArray(), 9, 3)
            .Concat(Enumerable.Repeat(double.NaN, line.Length - count)).ToArray();
        return Outputs(("Macd", line), ("Signal", signal), ("Histogram", line.Select((v, i) => v - signal[i]).ToArray()));
    }
}
