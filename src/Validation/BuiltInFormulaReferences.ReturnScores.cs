using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ReturnScoreOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? customerMeans = null)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 30); var kind = AverageKind(options, 1);
        var information = indicator.BatchName == IndicatorName.InformationRatio;
        var benchmark = ReferenceFraction.FromDouble(Math.Pow(1 + Number(options, information ? .05 : .02, "Bmk"), length / 360d) - 1);
        var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1);
        var returns = bars.Select((b, i) => i < length || bars[i - length].Close == 0 ? zero :
            RoundRocBankStage(ReferenceFraction.FromDouble(b.Close) / ReferenceFraction.FromDouble(bars[i - length].Close) - one - (information ? zero : benchmark))).ToArray();
        var means = customerMeans is null ? SmoothRocBankStage(returns, length, kind) : customerMeans.Select(ReferenceFraction.FromDouble).ToArray();
        var n = new ReferenceFraction(length);
        var output = returns.Select((_, i) =>
        {
            if (i < length - 1) return 0d;
            var window = Window(returns, i, length).ToArray();
            var average = window.Aggregate(zero, (a, b) => a + b) / n;
            var variance = window.Aggregate(zero, (a, b) => a + (b - average) * (b - average)) / n;
            var excess = (kind == 1 && customerMeans is null ? average : means[i]) - (information ? benchmark : zero);
            return variance.Sign == 0 ? 0 : excess.Sign * (excess * excess / variance).SqrtToDouble();
        }).ToArray();
        return Outputs((information ? "Ir" : "Sr", output));
    }
}
