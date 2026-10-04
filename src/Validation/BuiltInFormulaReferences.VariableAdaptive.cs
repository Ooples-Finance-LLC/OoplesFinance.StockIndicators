using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget VariableAdaptiveBudget = new(0, 4e-15, requireSameSign: true);
    internal static IReadOnlyDictionary<string, double[]> VariableAdaptiveOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return VariableAdaptiveValues(bars, Integer(options, "Length", 14),
            (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!, (indicator as IIndicator)?.Source is not null).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) VariableAdaptiveValues(
        IReadOnlyList<Bar> bars, int length, MovingAvgType kind, bool selected = false, IReadOnlyList<double>[]? replacements = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x); var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] source, IReadOnlyList<double>? replacement) => replacement is not null
            ? Enumerable.Range(0, bars.Count).Select(i => i < replacement.Count ? R(replacement[i]) : zero).ToArray()
            : RationalAverage(source, length, kind, AverageKind(new { MaType = kind }, 0));
        var prices = bars.Select(b => b.Close).ToArray(); var highs = bars.Select(b => b.High).ToArray(); var lows = bars.Select(b => b.Low).ToArray();
        if (selected) for (var i = 0; i < bars.Count; i++)
        {
            var tolerance = 1e-12 * Math.Max(Math.Abs(highs[i]), Math.Abs(lows[i]));
            if (prices[i] >= lows[i] - tolerance && prices[i] <= highs[i] + tolerance) continue;
            var previous = i == 0 ? prices[i] : prices[i-1]; highs[i] = Math.Max(prices[i], previous); lows[i] = Math.Min(prices[i], previous);
        }
        var c = Mean(prices.Select(R).ToArray(), replacements?[0]);
        var o = Mean(bars.Select(b => R(b.Open)).ToArray(), replacements?[1]);
        var h = Mean(highs.Select(R).ToArray(), replacements?[2]);
        var l = Mean(lows.Select(R).ToArray(), replacements?[3]);
        var values = new double[bars.Count]; var signals = new Signal[bars.Count]; var previousValue = zero; var previousPrice = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var width = h[i] - l[i]; var gain = width.Sign == 0 ? zero : (c[i] - o[i]).Abs() / width;
            if (width.Sign != 0)
            {
                if (gain.CompareTo(R(.01)) < 0) gain = R(.01);
                if (gain.CompareTo(R(.99)) > 0) gain = R(.99);
            }
            var price = R(prices[i]); var previous = i == 0 ? price : previousValue;
            var current = (R(1) - gain) * previous + gain * price;
            var margin = price - current; var oldMargin = previousPrice - previous; var change = margin.CompareTo(oldMargin);
            signals[i] = margin.Sign > 0 && change > 0 ? Signal.StrongBuy : margin.Sign < 0 && change < 0 ? Signal.StrongSell
                : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
            values[i] = current.ToDouble(); previousValue = current; previousPrice = price;
        }
        return (new Dictionary<string, double[]> { ["Vama"] = values }, signals);
    }
}
