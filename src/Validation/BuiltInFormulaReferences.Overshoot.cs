using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> OvershootOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 14));
        return OvershootOutputs(bars, length, AverageKind(options, 1));
    }
    internal static IReadOnlyDictionary<string, double[]> OvershootOutputs(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var means = SmoothRocBankStage(prices, length, kind); var indices = Enumerable.Range(0, bars.Count).Select(i => new ReferenceFraction(i)).ToArray();
        var indexMeans = SmoothRocBankStage(indices, length, kind); var errors = new ReferenceFraction[bars.Count]; var errorMeans = new ReferenceFraction[bars.Count];
        var previous = new ReferenceFraction(0); var result = new double[bars.Count]; var half = (int)((length + 1L) / 2);
        for (var i = 0; i < bars.Count; i++)
        {
            var prior = i == 0 ? new ReferenceFraction(0) : previous.Sign == 0 ? prices[i - 1] : previous;
            var difference = prior - prices[i]; errors[i] = (difference.Sign < 0 ? new ReferenceFraction(0) - difference : difference).RoundExtendedBinary64();
            var sum = new ReferenceFraction(0); var count = Math.Min(i + 1, half);
            for (var j = i - count + 1; j <= i; j++) sum += errors[j];
            errorMeans[i] = (sum / new ReferenceFraction(count)).RoundExtendedBinary64();
            var highest = errorMeans[i];
            for (var j = Math.Max(0, i - length + 1); j < i; j++) if ((errorMeans[j] - highest).Sign > 0) highest = errorMeans[j];
            var gain = highest.Sign == 0 ? new ReferenceFraction(0) : ReferenceFraction.FromDouble((errorMeans[i] / highest).ToDouble());
            var value = means[i];
            if (i + 1 >= length && length > 1)
            {
                var center = new ReferenceFraction(length - 1) / new ReferenceFraction(2); var covariance = new ReferenceFraction(0); var spread = covariance;
                for (var j = 0; j < length; j++) { var x = new ReferenceFraction(j) - center; covariance += x * prices[i - length + 1 + j]; spread += x * x; }
                value += covariance / spread * (indices[i] - indexMeans[i]) * gain;
            }
            previous = value.RoundExtendedBinary64(); result[i] = previous.ToDouble();
        }
        return Outputs(("Orma", result));
    }
}
