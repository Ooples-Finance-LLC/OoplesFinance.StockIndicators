using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedImpulseReference(IReadOnlyList<Bar> bars, object options,
        bool percentage, IReadOnlyList<double>? selected = null)
    {
        var length = Integer(options, "Length", 34);
        var kind = BoundedMeanKind(options, 6);
        var values = selected?.ToArray() ?? bars.Select(b => ((ReferenceFraction.FromDouble(b.High)
            + ReferenceFraction.FromDouble(b.Low) + ReferenceFraction.FromDouble(b.Close)) / new ReferenceFraction(3)).ToDouble()).ToArray();
        var first = RoundedEma(values, length);
        var second = RoundedEma(first, length);
        var high = RoundedBoundedStage(bars.Select(b => b.High).ToArray(), length, kind);
        var low = RoundedBoundedStage(bars.Select(b => b.Low).ToArray(), length, kind);
        var line = first.Select((v, i) =>
        {
            var middle = (new ReferenceFraction(2) * ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(second[i])).ToDouble();
            if (double.IsInfinity(middle)) return double.NaN;
            var boundary = middle > high[i] ? high[i] : middle < low[i] ? low[i] : middle;
            return !percentage ? (ReferenceFraction.FromDouble(middle) - ReferenceFraction.FromDouble(boundary)).ToDouble()
                : boundary == 0 ? 0 : (new ReferenceFraction(100) * (ReferenceFraction.FromDouble(middle) / ReferenceFraction.FromDouble(boundary) - new ReferenceFraction(1))).ToDouble();
        }).ToArray();
        var count = line.TakeWhile(v => !double.IsNaN(v) && !double.IsInfinity(v)).Count();
        var period = Integer(options, "SignalLength", 9);
        var signal = line.Select((_, i) =>
        {
            if (i >= count) return double.NaN;
            var window = Window(line, i, period).ToArray();
            var sum = new ReferenceFraction(0);
            foreach (var value in window) sum += ReferenceFraction.FromDouble(value);
            return (sum / new ReferenceFraction(window.Length)).ToDouble();
        }).ToArray();
        return Outputs((percentage ? "Ppo" : "Macd", line), ("Signal", signal), ("Histogram", line.Select((v, i) => v - signal[i]).ToArray()));
    }
}
