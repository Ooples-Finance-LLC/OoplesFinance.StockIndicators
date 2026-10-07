using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> FisherLsOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return FisherLsValues(bars, Integer(options, "Length", 100), AverageKind(options, 1)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) FisherLsValues(IReadOnlyList<Bar> bars, int length, int kind,
        double[]? externalPrices = null, double[]? externalIndices = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var indices = bars.Select((_, i) => R(i)).ToArray();
        var means = externalPrices is null ? SmoothRocBankStage(prices, length, kind) : externalPrices.Select(R).ToArray();
        var indexMeans = externalIndices is null ? SmoothRocBankStage(indices, length, kind) : externalIndices.Select(R).ToArray();
        var residuals = new ReferenceFraction[bars.Count]; var estimates = new ReferenceFraction[bars.Count]; var signals = new Signal[bars.Count];
        ReferenceFraction Variance(ReferenceFraction[] values, int index)
        {
            if (index + 1 < length) return R(0);
            var sample = Window(values, index, length).ToArray(); var center = sample.Aggregate(R(0), (a, b) => a + b) / R(sample.Length);
            return sample.Aggregate(R(0), (a, b) => a + (b - center) * (b - center)) / R(sample.Length);
        }
        for (var i = 0; i < bars.Count; i++)
        {
            residuals[i] = (prices[i] - (i == 0 ? prices[i] : estimates[i - 1])).RoundExtendedBinary64();
            var sample = Window(residuals, i, length).ToArray(); var total = sample.Aggregate(R(0), (a, b) => a + (b.Sign < 0 ? R(0) - b : b));
            var signed = sample.Aggregate(R(0), (a, b) => a + b);
            var rho = total.Sign == 0 ? R(0) : R(Math.Tanh((signed / total).ToDouble()));
            var priceVariance = Variance(prices, i); var indexVariance = Variance(indices, i); var correction = R(0);
            if (indexVariance.Sign > 0 && priceVariance.Sign > 0)
            {
                var factor = (indices[i] - indexMeans[i]) * rho; var square = factor * factor * priceVariance / indexVariance;
                var root = square.SqrtToDouble(); var scale = R(1); var step = new ReferenceFraction(BigInteger.One << 512);
                while (double.IsInfinity(root)) { square /= step * step; scale *= step; root = square.SqrtToDouble(); }
                correction = R(factor.Sign < 0 ? -root : root) * scale;
            }
            estimates[i] = (means[i] + correction).RoundExtendedBinary64();
            var margin = prices[i] - estimates[i]; var previous = i == 0 ? R(0) - prices[i] : prices[i - 1] - estimates[i - 1];
            signals[i] = margin.Sign > 0 && margin.CompareTo(previous) > 0 ? Signal.StrongBuy : margin.Sign < 0 && margin.CompareTo(previous) < 0 ? Signal.StrongSell
                : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { ["Flsma"] = estimates.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
