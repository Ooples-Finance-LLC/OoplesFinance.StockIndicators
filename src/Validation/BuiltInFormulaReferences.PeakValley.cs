using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> PeakValleyOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return PeakValleyValues(bars, Integer(options, "Length", 500), Integer(options, "SmoothLength", 100), AverageKind(options, 1));
    }
    internal static Dictionary<string, double[]> PeakValleyValues(IReadOnlyList<Bar> bars, int length, int smooth, int kind = 1, double[]? selected = null, double[]? externalMean = null)
    {
        length = Math.Max(1, length); smooth = Math.Max(1, smooth);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var zero = R(0); var one = R(1); var prices = (selected ?? Closes(bars)).Select(R).ToArray();
        var means = new ReferenceFraction[prices.Length];
        var priceSum = new ReferenceFraction[prices.Length + 1]; var priceWeighted = new ReferenceFraction[prices.Length + 1];
        priceSum[0] = priceWeighted[0] = zero;
        for (var i = 0; i < prices.Length; i++) { priceSum[i + 1] = priceSum[i] + prices[i]; priceWeighted[i + 1] = priceWeighted[i] + prices[i] * R(i); }
        var fallback = externalMean ?? (kind is not (1 or 2 or 3 or 6) ? Average(prices.Select(p => p.ToDouble()).ToArray(), length, kind) : null);
        for (var i = 0; i < prices.Length; i++)
        {
            if (fallback is not null) { means[i] = R(fallback[i]); continue; }
            var previous = i == 0 ? zero : means[i - 1];
            if (kind == 6) means[i] = (previous * R(length - 1) + prices[i]) / R(length);
            else if (kind == 3 && i >= length) means[i] = (previous * R(length - 1) + R(2) * prices[i]) / R(length + 1L);
            else if (kind == 1 && i + 1 < length) means[i] = zero;
            else
            {
                var start = Math.Max(0, i - length + 1);
                var sum = priceSum[i + 1] - priceSum[start];
                var weighted = priceWeighted[i + 1] - priceWeighted[start] + R((long)length - i) * sum;
                means[i] = kind == 2 ? weighted / new ReferenceFraction((long)length * (length + 1L) / 2) : sum / R(Math.Min(i + 1, length));
            }
        }
        var offsets = prices.Select((p, i) => p - means[i]).ToArray();
        var fits = new ReferenceFraction[prices.Length]; var ratios = new ReferenceFraction[prices.Length];
        var residualSum = new ReferenceFraction[prices.Length + 1]; var residualWeighted = new ReferenceFraction[prices.Length + 1];
        residualSum[0] = residualWeighted[0] = zero;
        for (var i = 0; i < offsets.Length; i++)
        { var magnitude = offsets[i].Sign < 0 ? zero - offsets[i] : offsets[i]; residualSum[i + 1] = residualSum[i] + magnitude; residualWeighted[i + 1] = residualWeighted[i] + R(i) * magnitude; }
        var extrema = new SortedSet<(ReferenceFraction Value, int Index)>();
        var sign1 = new double[prices.Length]; var sign2 = new double[prices.Length]; var sign3 = new double[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            var start = Math.Max(0, i - smooth + 1); var n = R(i - start + 1);
            var total = residualSum[i + 1] - residualSum[start]; var yMean = total / n;
            var xMean = (n - one) / R(2);
            // Prefix moments solve the same centered normal equations without rescanning the window.
            var covariance = residualWeighted[i + 1] - residualWeighted[start] - (R(start) + xMean) * total;
            var variance = n * (n * n - one) / R(12);
            fits[i] = variance.Sign == 0 ? yMean : yMean + covariance / variance * xMean;
            var highLength = Math.Max(2, length);
            if (i >= highLength) extrema.Remove((fits[i - highLength], i - highLength));
            extrema.Add((fits[i], i)); var high = extrema.Max.Value;
            ratios[i] = high.Sign == 0 ? zero : fits[i] / high;
            var prior = i == 0 ? zero : ratios[i - 1]; var direction = -(double)offsets[i].Sign;
            sign1[i] = ratios[i].CompareTo(one) == 0 && prior.CompareTo(one) != 0 ? direction : 0;
            sign2[i] = ratios[i].CompareTo(R(4) / R(5)) < 0 ? direction : 0;
            sign3[i] = prior.CompareTo(one) == 0 && ratios[i].CompareTo(prior) < 0 ? direction : 0;
        }
        return new() { ["Sign1"] = sign1, ["Sign2"] = sign2, ["Sign3"] = sign3 };
    }
}
