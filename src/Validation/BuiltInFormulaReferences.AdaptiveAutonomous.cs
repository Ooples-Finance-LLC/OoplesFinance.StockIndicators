using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AdaptiveAutonomousOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return AdaptiveAutonomousOutputs(bars, Math.Max(1, Integer(options, "Length", 14)), Number(options, 3, "Lambda", "Gamma"), indicator.BatchName == IndicatorName.AdaptiveAutonomousRecursiveTrailingStop);
    }
    internal static IReadOnlyDictionary<string, double[]> AdaptiveAutonomousOutputs(IReadOnlyList<Bar> bars, int length, double gamma, bool trailing)
    {
        var zero = new ReferenceFraction(0); var scale = ReferenceFraction.FromDouble(gamma);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        ReferenceFraction Abs(ReferenceFraction v) => v.Sign < 0 ? zero - v : v;
        ReferenceFraction Round(ReferenceFraction v) => v.RoundExtendedBinary64();
        var first = zero; var second = zero; var sum = zero; var upper = zero; var lower = zero; var rising = false;
        var averages = new double[bars.Count]; var widths = new double[bars.Count]; var stops = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            if (i == 0) first = second = prices[i];
            sum = Round(sum + Round(Abs(prices[i] - second)));
            var width = i == 0 ? zero : Round(Round(sum / new ReferenceFraction(i)) * scale);
            var travel = zero;
            if (i >= length) for (var j = i - length + 1; j <= i; j++) travel += Abs(prices[j] - prices[j - 1]);
            var gain = travel.Sign == 0 ? zero : Round(Abs(prices[i] - prices[i - length]) / travel);
            var target = (prices[i] - second - width).Sign > 0 ? Round(prices[i] + width) : (prices[i] - second + width).Sign < 0 ? Round(prices[i] - width) : second;
            first = Round(first + gain * (target - first)); second = Round(second + gain * (first - second));
            if ((prices[i] - upper).Sign > 0) rising = true; else if ((prices[i] - lower).Sign < 0) rising = false;
            upper = Round(second + width); lower = Round(second - width);
            averages[i] = second.ToDouble(); widths[i] = width.ToDouble(); stops[i] = (rising ? lower : upper).ToDouble();
        }
        return trailing ? Outputs(("Ts", stops)) : Outputs(("D", widths), ("Aarma", averages));
    }
}
