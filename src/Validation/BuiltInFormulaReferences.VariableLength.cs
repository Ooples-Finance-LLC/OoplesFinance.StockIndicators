using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VariableLengthOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return VariableLengthValues(bars, Integer(o, "Length", 5), Integer(o, "MaxLength", 50), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) VariableLengthValues(IReadOnlyList<Bar> bars, int minimum, int maximum, MovingAvgType kind, double[]? custom = null)
    {
        minimum = Math.Max(1, minimum); maximum = Math.Max(minimum, maximum); ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x); var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int length) => RationalAverage(values, length, kind);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var means = custom is null ? Mean(prices, maximum) : prices.Select((_, i) => R(i < custom.Length ? custom[i] : 0)).ToArray();
        var line = new ReferenceFraction[bars.Count]; var lengths = new double[bars.Count]; var signals = new Signal[bars.Count]; var period = (long)maximum;
        for (var i = 0; i < bars.Count; i++)
        {
            if (i + 1 >= maximum)
            {
                var center = zero; for (var j = i - maximum + 1; j <= i; j++) center += prices[j]; center /= R(maximum);
                var variance = zero; for (var j = i - maximum + 1; j <= i; j++) { var residual = prices[j] - center; variance += residual * residual; } variance /= R(maximum);
                if (variance.Sign > 0)
                {
                    var delta = prices[i] - means[i]; var scoreSquare = delta * delta / variance;
                    period += scoreSquare.CompareTo(R(1) / R(16)) <= 0 ? 1 : scoreSquare.CompareTo(R(49) / R(16)) > 0 ? -1 : 0;
                    period = Math.Max(minimum, Math.Min(maximum, period));
                }
            }
            lengths[i] = period; var prior = i == 0 ? prices[i] : line[i - 1]; line[i] = prior + R(2) / R(period + 1) * (prices[i] - prior);
            var difference = prices[i] - line[i]; var previousDifference = i == 0 ? zero - prices[i] : prices[i - 1] - line[i - 1];
            signals[i] = difference.Sign > 0 && difference.CompareTo(previousDifference) > 0 ? Signal.StrongBuy : difference.Sign < 0 && difference.CompareTo(previousDifference) < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new() { ["Length"] = lengths, ["Vlma"] = line.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
