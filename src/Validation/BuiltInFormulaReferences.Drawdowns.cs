using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DrawdownOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? customerMeans = null)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 14);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var zero = new ReferenceFraction(0); var hundred = new ReferenceFraction(100);
        var drawdowns = prices.Select((price, i) =>
        {
            var high = ReferenceFraction.FromDouble(Window(bars, i, length).Max(b => b.Close));
            return high.Sign == 0 ? zero : RoundRocBankStage(hundred * (price - high) / high);
        }).ToArray();
        var riskSquared = drawdowns.Select((_, i) =>
        {
            var window = Window(drawdowns, i, length).ToArray();
            return window.Aggregate(zero, (a, b) => a + b * b) / new ReferenceFraction(window.Length);
        }).ToArray();
        if (indicator.BatchName == IndicatorName.UlcerIndex) return Outputs(("Ui", riskSquared.Select(v => v.SqrtToDouble()).ToArray()));
        var benchmark = ReferenceFraction.FromDouble(Math.Pow(1 + Number(options, .02, "Bmk"), length / 360d) - 1);
        var returns = prices.Select((price, i) => i < length || prices[i - length].Sign == 0 ? zero :
            RoundRocBankStage(hundred * (price / prices[i - length] - new ReferenceFraction(1) - benchmark))).ToArray();
        var means = customerMeans is null ? SmoothRocBankStage(returns, length, AverageKind(options, 1)) : customerMeans.Select(ReferenceFraction.FromDouble).ToArray();
        return Outputs(("Mr", means.Select((mean, i) => riskSquared[i].Sign == 0 ? 0 : mean.Sign * (mean * mean / riskSquared[i]).SqrtToDouble()).ToArray()));
    }
}
