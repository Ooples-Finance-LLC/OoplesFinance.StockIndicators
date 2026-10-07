using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SortinoOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator,
        double[]? customerMeans = null, double[]? customerDownside = null)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 30); var kind = AverageKind(options, 1);
        var target = ReferenceFraction.FromDouble(Math.Pow(1 + Number(options, .02, "Bmk"), length / 360d) - 1);
        var zero = new ReferenceFraction(0); var factor = ReferenceFraction.FromDouble(Math.Pow(2, 537)); var scale = factor * factor * factor * factor;
        var returns = bars.Select((b, i) => i < length || bars[i - length].Close == 0 ? zero :
            RoundRocBankStage(ReferenceFraction.FromDouble(b.Close) / ReferenceFraction.FromDouble(bars[i - length].Close) - new ReferenceFraction(1) - target)).ToArray();
        var means = customerMeans is null ? SmoothRocBankStage(returns, length, kind) : customerMeans.Select(ReferenceFraction.FromDouble).ToArray();
        var squares = returns.Select(v => v.Sign >= 0 ? zero : RoundRocBankStage(v * v * scale)).ToArray();
        var downside = customerDownside is null ? SmoothRocBankStage(squares, length, kind).Select(v => v / scale).ToArray() : customerDownside.Select(ReferenceFraction.FromDouble).ToArray();
        var output = means.Select((mean, i) => downside[i].Sign <= 0 ? 0 : mean.Sign * (mean * mean / downside[i]).SqrtToDouble()).ToArray();
        return Outputs(("Sr", output));
    }
}
