using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    // Scaling the negative logistic tail before exp introduces at most an exponent ulp.
    // One subnormal ulp is allowed, but same-sign checking forbids erasing a nonzero tail.
    internal static readonly IndicatorErrorBudget ZScoreLogisticBudget = new(double.Epsilon, 2e-13, requireSameSign: true);
    internal static IReadOnlyDictionary<string, double[]> ZScoreOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName; var options = indicator.CreateOptions();
        var fast = name is IndicatorName.FastZScore or IndicatorName.InverseFisherFastZScore;
        var inverse = name is IndicatorName.InverseFisherZScore or IndicatorName.InverseFisherFastZScore;
        var key = fast ? inverse ? "Iffzs" : "Fzs" : inverse ? "Ifzs" : "Zscore";
        var values = StandardizedValues(Closes(bars), Integer(options, "Length", fast ? 200 : 14), AverageKind(options, 1), fast);
        if (inverse) values = values.Select(v => double.IsInfinity(v) ? fast ? Math.Sign(v) : v > 0 ? 100d : 0d
            : fast ? ReferenceFraction.FromDouble(5 * v).TanhToDouble()
            : ReferenceFraction.FromDouble(v).LogisticPercentToDouble()).ToArray();
        return Outputs((key, values));
    }
    internal static double[] StandardizedValues(double[] prices, int length, int kind, bool fast, double[]? customerMeans = null)
    {
        length = Math.Max(1, length);
        var source = prices.Select(ReferenceFraction.FromDouble).ToArray();
        var means = customerMeans is null ? SmoothStrengthStage(source, length, kind) : customerMeans.Select(ReferenceFraction.FromDouble).ToArray();
        var series = fast ? means : source; var result = new double[prices.Length];
        // Exact prefix moments avoid repeatedly rebuilding rational regression/deviation windows.
        var sums = new ReferenceFraction[series.Length + 1]; var squares = new ReferenceFraction[series.Length + 1]; var weighted = new ReferenceFraction[series.Length + 1];
        sums[0] = squares[0] = weighted[0] = new ReferenceFraction(0);
        for (var i = 0; i < series.Length; i++) { sums[i + 1] = sums[i] + series[i]; squares[i + 1] = squares[i] + series[i] * series[i]; weighted[i + 1] = weighted[i] + new ReferenceFraction(i) * series[i]; }
        ReferenceFraction Fit(int end, int period)
        {
            var count = Math.Min(end + 1, period); var first = end - count + 1; var n = new ReferenceFraction(count);
            var sum = sums[end + 1] - sums[first]; var mean = sum / n;
            if (count == 1) return mean;
            var center = new ReferenceFraction(count - 1) / new ReferenceFraction(2);
            var xy = weighted[end + 1] - weighted[first] - (new ReferenceFraction(first) + center) * sum;
            var xx = n * (n * n - new ReferenceFraction(1)) / new ReferenceFraction(12);
            return mean + center * xy / xx;
        }
        for (var i = length - 1; i < result.Length; i++)
        {
            var first = i - length + 1; var n = new ReferenceFraction(length);
            var mean = (sums[i + 1] - sums[first]) / n;
            var variance = (squares[i + 1] - squares[first]) / n - mean * mean;
            var residual = fast ? (Fit(i, Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d)))) - Fit(i, length)) / new ReferenceFraction(2)
                : source[i] - (kind == 1 && customerMeans is null ? mean : means[i]);
            result[i] = variance.Sign == 0 ? 0 : residual.Sign * (residual * residual / variance).SqrtToDouble();
        }
        return result;
    }
}
