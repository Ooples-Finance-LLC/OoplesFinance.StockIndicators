using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VerticalHorizontalAverageOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = Math.Max(1, Integer(indicator.CreateOptions(), "Length", 50));
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var changes = new ReferenceFraction[bars.Count]; var values = new double[bars.Count];
        var zero = new ReferenceFraction(0); var previous = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var difference = prices[i] - (i >= length ? prices[i - length] : zero); changes[i] = difference.Sign < 0 ? zero - difference : difference;
            var high = bars[i].Close; var low = high; var path = zero;
            for (var j = Math.Max(0, i - length + 1); j <= i; j++)
            {
                high = Math.Max(high, bars[j].Close); low = Math.Min(low, bars[j].Close); path += changes[j];
            }
            var ratio = path.Sign == 0 ? zero : ((ReferenceFraction.FromDouble(high) - ReferenceFraction.FromDouble(low)) / path).RoundExtendedBinary64();
            var gain = (ratio * ratio).RoundExtendedBinary64();
            if (i == 0) previous = prices[i];
            previous = (previous + gain * (prices[i] - previous)).RoundExtendedBinary64(); values[i] = previous.ToDouble();
        }
        return Outputs(("Vhma", values));
    }
}
