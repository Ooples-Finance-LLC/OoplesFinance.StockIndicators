using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TargetReturnsOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 30);
        var target = ReferenceFraction.FromDouble(Math.Pow(1 + Number(options, .05, "Bmk"), length / 360d) - 1);
        var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1);
        var potential = indicator.BatchName == IndicatorName.UpsidePotentialRatio;
        var returns = bars.Select((b, i) => i < length || bars[i - length].Close == 0 ? zero :
            ReferenceFraction.FromDouble(b.Close) / ReferenceFraction.FromDouble(bars[i - length].Close) - one).ToArray();
        var output = returns.Select((_, i) =>
        {
            var window = Window(returns, i, length).ToArray();
            if (indicator.BatchName == IndicatorName.TreynorRatio)
            {
                var beta = ReferenceFraction.FromDouble(Number(options, 1, "Beta"));
                var mean = window.Aggregate(zero, (a, b) => a + b) / new ReferenceFraction(window.Length);
                return beta.Sign == 0 ? 0 : ((mean - target) / beta).ToDouble();
            }
            var differences = Enumerable.Repeat(zero, length - window.Length).Concat(window).Select(v => v - target).ToArray();
            var up = differences.Where(v => v.Sign > 0).Aggregate(zero, (a, b) => a + b);
            var down = differences.Where(v => v.Sign < 0).Aggregate(zero, (a, b) => a + (potential ? b * b : zero - b));
            if (down.Sign == 0) return 0;
            return potential ? (up * up / (new ReferenceFraction(length) * down)).SqrtToDouble() : (up / down).ToDouble();
        }).ToArray();
        return Outputs((indicator.BatchName == IndicatorName.TreynorRatio ? "Tr" : potential ? "Upr" : "Or", output));
    }
}
