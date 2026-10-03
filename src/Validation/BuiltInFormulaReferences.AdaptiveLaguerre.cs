using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] AdaptiveLaguerreValues(IReadOnlyList<Bar> bars, int length, int medianLength = 5)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var chunk = new ReferenceFraction(BigInteger.One << 512); var upper = new ReferenceFraction(BigInteger.One << 256); var lower = new ReferenceFraction(1) / upper;
        ReferenceFraction Round(ReferenceFraction value)
        {
            if (value.Sign == 0) return value; var scale = new ReferenceFraction(1);
            while (value.Abs().CompareTo(lower) < 0) { value *= chunk; scale /= chunk; }
            while (value.Abs().CompareTo(upper) >= 0) { value /= chunk; scale *= chunk; }
            return R(value.ToDouble()) * scale;
        }
        length = Math.Max(1, length); medianLength = Math.Max(1, medianLength);
        var deviations = new ReferenceFraction[bars.Count]; var ranks = new double[bars.Count]; var filters = new ReferenceFraction[bars.Count]; var output = new double[bars.Count];
        var stages = new ReferenceFraction[4]; var gain = 2d / (length + 1d); var one = new ReferenceFraction(1);
        for (var i = 0; i < bars.Count; i++)
        {
            var price = R(bars[i].Close); var prior = i == 0 ? price : filters[i - 1];
            if (i == 0) for (var j = 0; j < 4; j++) stages[j] = price;
            deviations[i] = Round(price - prior).Abs(); var start = Math.Max(0, i - length + 1); var low = deviations[i]; var high = low;
            for (var j = start; j < i; j++) { if (deviations[j].CompareTo(low) < 0) low = deviations[j]; if (deviations[j].CompareTo(high) > 0) high = deviations[j]; }
            var scale = price.Abs().CompareTo(prior.Abs()) >= 0 ? price.Abs() : prior.Abs(); var threshold = R(7.105427357601002e-15) * scale;
            ranks[i] = (high - low).CompareTo(threshold) <= 0 || (deviations[i] - low).CompareTo(threshold) <= 0 ? 0
                : (high - deviations[i]).CompareTo(threshold) <= 0 ? 1 : ((deviations[i] - low) / (high - low)).ToDouble();
            if (ranks[i] != 0)
            {
                var sorted = ranks.Skip(Math.Max(0, i - medianLength + 1)).Take(Math.Min(i + 1, medianLength)).OrderBy(x => x).ToArray();
                gain = ((R(sorted[(sorted.Length - 1) / 2]) + R(sorted[sorted.Length / 2])) / new ReferenceFraction(2)).ToDouble();
            }
            var g = R(gain); var next = new ReferenceFraction[4]; next[0] = Round(g * price + (one - g) * stages[0]);
            for (var j = 1; j < 4; j++) next[j] = Round(stages[j - 1] + (one - g) * (stages[j] - next[j - 1]));
            filters[i] = Round((next[0] + new ReferenceFraction(2) * next[1] + new ReferenceFraction(2) * next[2] + next[3]) / new ReferenceFraction(6));
            output[i] = filters[i].ToDouble(); stages = next;
        }
        return output;
    }
}
