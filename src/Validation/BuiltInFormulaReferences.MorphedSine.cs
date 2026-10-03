using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> MorphedSineOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => new() { ["Msw"] = MorphedSineValues(Closes(bars), Integer(indicator.CreateOptions(), "Length", 14), 100).Values };
    internal static (double[] Values, Signal[] Signals) MorphedSineValues(double[] prices, int length, double power)
    {
        length = Math.Max(1, length); var divisor = ReferenceFraction.FromDouble(power);
        var line = prices.Select((price, i) => ReferenceFraction.FromDouble(price)
            + ReferenceFraction.FromDouble(DegreeSine(2L * i, length)) / divisor).ToArray();
        var signals = new Signal[prices.Length]; var zero = new ReferenceFraction(0);
        for (var i = 0; i < prices.Length; i++)
        {
            var slope = line[i] - (i > 0 ? line[i - 1] : zero);
            var previousSlope = i > 0 ? line[i - 1] - (i > 1 ? line[i - 2] : zero) : zero;
            var change = slope.CompareTo(previousSlope);
            signals[i] = slope.Sign > 0 && change > 0 ? Signal.StrongBuy : slope.Sign < 0 && change < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (line.Select(v => v.ToDouble()).ToArray(), signals);
    }
}
