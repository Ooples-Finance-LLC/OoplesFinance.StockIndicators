using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TrendContinuationOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = Integer(indicator.CreateOptions(), "Length", 35); var zero = new ReferenceFraction(0);
        var plusRun = zero; var minusRun = zero;
        var positive = new ReferenceFraction[bars.Count]; var negative = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var change = i == 0 ? zero : RoundRocBankStage(ReferenceFraction.FromDouble(bars[i].Close) - ReferenceFraction.FromDouble(bars[i - 1].Close));
            var up = change.Sign > 0 ? change : zero; var down = change.Sign < 0 ? change.Abs() : zero;
            plusRun = up.Sign == 0 ? zero : RoundRocBankStage(plusRun + up);
            minusRun = down.Sign == 0 ? zero : RoundRocBankStage(minusRun + down);
            positive[i] = RoundRocBankStage(up - minusRun); negative[i] = RoundRocBankStage(down - plusRun);
        }
        double[] Sum(ReferenceFraction[] values) => values.Select((_, i) => values.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).Aggregate(zero, (sum, v) => sum + v).ToDouble()).ToArray();
        return Outputs(("TcfPlus", Sum(positive)), ("TcfMinus", Sum(negative)));
    }
}
