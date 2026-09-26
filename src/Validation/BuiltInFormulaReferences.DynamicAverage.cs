using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DynamicAverageOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var fast = Math.Max(1, Integer(options, "FastLength", 6)); var slow = Math.Max(1, Integer(options, "SlowLength", 200));
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var zero = new ReferenceFraction(0);
        ReferenceFraction Variance(int end, int length)
        {
            if (end < length - 1) return zero;
            var sum = zero; for (var j = end - length + 1; j <= end; j++) sum += prices[j];
            var mean = sum / new ReferenceFraction(length); var square = zero;
            for (var j = end - length + 1; j <= end; j++) { var delta = prices[j] - mean; square += delta * delta; }
            return square / new ReferenceFraction(length);
        }
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var shortVariance = Variance(i, fast); var longVariance = Variance(i, slow);
            var period = Math.Min(fast, slow);
            if (shortVariance.Sign > 0 && slow > fast)
            {
                var ratio = longVariance / shortVariance;
                // Find the first midpoint above the exact positive square root.
                var lower = fast; var upper = slow;
                while (lower < upper)
                {
                    var candidate = lower + (upper - lower) / 2;
                    var midpoint = new ReferenceFraction(2L * (candidate - fast) + 1) / new ReferenceFraction(2);
                    var relation = ratio.CompareTo(midpoint * midpoint);
                    if (relation < 0 || relation == 0 && candidate % 2 == 0) upper = candidate;
                    else lower = candidate + 1;
                }
                period = lower;
            }
            var total = zero;
            for (var j = Math.Max(0, i - period + 1); j <= i; j++) total += prices[j];
            output[i] = (total / new ReferenceFraction(period)).ToDouble();
        }
        return Outputs(("Dama", output));
    }
}
