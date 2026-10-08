using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] MarketDirectionOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return MarketDirectionValues(bars, Integer(o, "FastLength", Integer(o, "Length", 13)), Integer(o, "SlowLength", 55)).Values; }
    internal static (double[] Values, Signal[] Signals) MarketDirectionValues(IReadOnlyList<Bar> bars, int fast, int slow)
    {
        fast = Math.Max(1, fast); slow = Math.Max(1, slow);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var values = new double[bars.Count]; var signals = new Signal[bars.Count];
        var previousCrossing = R(0); var previousOutput = R(0);
        ReferenceFraction Sum(int i, int period)
        { var total = R(0); for (var j = Math.Max(0, i - period + 1); j <= i; j++) total += prices[j]; return total; }
        for (var i = 0; i < bars.Count; i++)
        {
            // Reference explicitly constructs each crossing as a rational;
            // production retains its common numerator on an integer price grid.
            var crossing = fast == slow ? R(0) : (R(fast) * Sum(i, slow - 1) - R(slow) * Sum(i, fast - 1)) / R((long)slow - fast);
            var midpoint = (prices[i] + (i == 0 ? R(0) : prices[i - 1])) / R(2);
            var output = midpoint.Sign == 0 ? R(0) : R(100) * (previousCrossing - crossing) / midpoint;
            signals[i] = output.Sign > 0 && output.CompareTo(previousOutput) > 0 ? Signal.StrongBuy : output.Sign < 0 && output.CompareTo(previousOutput) < 0 ? Signal.StrongSell
                : output.Sign > 0 ? Signal.Buy : output.Sign < 0 ? Signal.Sell : Signal.None;
            values[i] = output.ToDouble(); previousCrossing = crossing; previousOutput = output;
        }
        return (values, signals);
    }
}
