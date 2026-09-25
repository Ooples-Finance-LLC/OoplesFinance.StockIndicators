namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    // Normalization stages are once-rounded. The log and recursive output allow a
    // small accumulated transcendental error; a separate zero-absolute test guards tiny signals.
    internal static readonly IndicatorErrorBudget FisherBudget = new(8e-15, 8e-15, requireSameSign: true);
    internal static double FisherReferenceTransform(double value)
    {
        var x = ReferenceFraction.FromDouble(value);
        if (Math.Abs(value) < 1d / 134217728) return value;
        var one = new ReferenceFraction(1);
        return .5 * ((one + x) / (one - x)).LogToDouble();
    }
    internal static double[] FisherValues(double[] prices, int length)
    {
        length = Math.Max(1, length);
        var result = new double[prices.Length]; double normalized = 0;
        var half = ReferenceFraction.FromDouble(.5);
        var gain = ReferenceFraction.FromDouble(.33 * 2);
        var retention = ReferenceFraction.FromDouble(.67);
        ReferenceFraction Round(ReferenceFraction x) => ReferenceFraction.FromDouble(x.ToDouble());
        for (var i = 0; i < prices.Length; i++)
        {
            var window = prices.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).Select(ReferenceFraction.FromDouble).ToArray();
            var low = window.Min(); var high = window.Max();
            var ratio = high.CompareTo(low) == 0 ? half : Round((ReferenceFraction.FromDouble(prices[i]) - low) / (high - low));
            normalized = (Round(gain * Round(ratio - half)) + Round(retention * ReferenceFraction.FromDouble(normalized))).ToDouble();
            normalized = Math.Max(-.999, Math.Min(.999, normalized));
            var transform = ReferenceFraction.FromDouble(FisherReferenceTransform(normalized));
            result[i] = (transform + Round(half * ReferenceFraction.FromDouble(i == 0 ? 0 : result[i - 1]))).ToDouble();
        }
        return result;
    }
}
