using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> CorrectedAverageOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return CorrectedAverageValues(bars, Integer(options, "Length", 35), AverageKind(options, 1)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) CorrectedAverageValues(IReadOnlyList<Bar> bars, int length, int kind, double[]? external = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var means = external is not null ? external.Select(R).ToArray() : kind is 1 or 2 or 3 or 6 ? SmoothRocBankStage(prices, length, kind) : Average(bars.Select(b => b.Close).ToArray(), length, kind).Select(R).ToArray();
        var outputs = new double[bars.Count]; var signals = new Signal[bars.Count]; var previous = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var variance = R(0);
            if (i + 1 >= length)
            {
                // Deliberately centered rational deviations, independent of the
                // production running integer sums and squares.
                var window = prices.Skip(i + 1 - length).Take(length).ToArray(); var center = window.Aggregate(R(0), (sum, p) => sum + p) / R(length);
                var population = window.Aggregate(R(0), (sum, p) => sum + (p - center) * (p - center)) / R(length);
                var sigma = R(population.SqrtToDouble()); variance = (sigma * sigma).RoundExtendedBinary64();
            }
            if (i == 0) previous = means[i];
            var displacement = (means[i] - previous).RoundExtendedBinary64(); var distance = (displacement * displacement).RoundExtendedBinary64();
            var gain = variance.Sign == 0 ? 1 : distance.CompareTo(variance) <= 0 ? 0 : 1 - (variance / distance).ToDouble();
            var line = i < length ? means[i] : (previous + (R(gain) * displacement).RoundExtendedBinary64()).RoundExtendedBinary64();
            outputs[i] = line.ToDouble(); var spread = prices[i] - line; var oldSpread = (i == 0 ? R(0) : prices[i - 1]) - previous;
            signals[i] = spread.Sign > 0 && spread.CompareTo(oldSpread) > 0 ? Signal.StrongBuy : spread.Sign < 0 && spread.CompareTo(oldSpread) < 0 ? Signal.StrongSell
                : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
            previous = line;
        }
        return (new Dictionary<string, double[]> { ["Cma"] = outputs }, signals);
    }
}
