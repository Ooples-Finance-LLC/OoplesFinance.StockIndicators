using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EquityOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? customerAverage = null)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 14)); var kind = AverageKind(options, 1);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var averages = customerAverage is null ? SmoothRocBankStage(prices, length, kind) : customerAverage.Select(ReferenceFraction.FromDouble).ToArray();
        var changes = new ReferenceFraction[bars.Count]; var cumulative = new ReferenceFraction(0); var result = new double[bars.Count]; var previousSide = 0;
        for (var i = 0; i < bars.Count; i++)
        {
            var side = (prices[i] - averages[i]).Sign;
            var delta = i == 0 ? new ReferenceFraction(0) : (prices[i] - prices[i - 1]).RoundExtendedBinary64();
            changes[i] = delta * new ReferenceFraction(previousSide); cumulative += delta * new ReferenceFraction(side);
            var requested = new ReferenceFraction(0);
            for (var j = Math.Max(0, i - length + 1); j <= i; j++) requested += changes[j];
            var gain = ReferenceFraction.FromDouble(cumulative.Sign == 0 ? .99 : Math.Max(.01, Math.Min(.99, (requested / cumulative).ToDouble())));
            var previous = i == 0 ? prices[i] : ReferenceFraction.FromDouble(result[i - 1]);
            result[i] = (gain * prices[i] + (new ReferenceFraction(1) - gain) * previous).ToDouble(); previousSide = side;
        }
        return Outputs(("Eqma", result));
    }
}
