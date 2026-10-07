using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> KwanOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return KwanValues(bars, Integer(o, "Length", 9), Integer(o, "SmoothLength", 2), AverageKind(o, 6)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) KwanValues(IReadOnlyList<Bar> bars, int length, int delay, int kind)
    {
        length = Math.Max(1, length); delay = Math.Max(1, delay);
        var strength = RoundedPriceRsi(bars, length, kind); var zero = new ReferenceFraction(0);
        var ratios = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            ratios[i] = zero;
            if (i < length || bars[i].Close == 0 || bars[i - length].Close == 0) continue;
            var window = Window(bars, i, length).ToArray(); var low = window.Min(b => b.Low); var high = window.Max(b => b.High);
            if (high == low) continue; // NOSONAR: S1244 - Exact zero price/range singularity guards; every distinct finite bound or nonzero price must retain its formula.
            // Independent rational stochastic divided by rational momentum.
            var stochastic = (ReferenceFraction.FromDouble(bars[i].Close) - ReferenceFraction.FromDouble(low))
                / (ReferenceFraction.FromDouble(high) - ReferenceFraction.FromDouble(low)) * new ReferenceFraction(100);
            var momentum = ReferenceFraction.FromDouble(bars[i].Close) / ReferenceFraction.FromDouble(bars[i - length].Close) * new ReferenceFraction(100);
            ratios[i] = (stochastic * ReferenceFraction.FromDouble(strength[i]) / momentum).RoundExtendedBinary64(); // NOSONAR: S4143 - The initialized zero is retained on early-continue paths; this assignment handles the remaining nonzero domain.
        }
        var output = new double[bars.Count]; var signals = new Signal[bars.Count]; var sum = zero;
        var previousIncrement = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var increment = i < delay ? zero : ratios[i - delay]; sum += increment;
            output[i] = (sum / new ReferenceFraction(delay)).ToDouble();
            signals[i] = increment.Sign > 0 && increment.CompareTo(previousIncrement) > 0 ? Signal.StrongBuy
                : increment.Sign < 0 && increment.CompareTo(previousIncrement) < 0 ? Signal.StrongSell
                : increment.Sign > 0 ? Signal.Buy : increment.Sign < 0 ? Signal.Sell : Signal.None;
            previousIncrement = increment;
        }
        return (new Dictionary<string, double[]> { ["Ki"] = output }, signals);
    }
}
