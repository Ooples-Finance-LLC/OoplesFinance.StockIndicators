using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] SuperTrendFilterOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return SuperTrendFilterValues(bars, Integer(options, "Length", 200), Number(options, .9, "Factor")).Values; }
    internal static (double[] Values, Signal[] Signals) SuperTrendFilterValues(IReadOnlyList<Bar> bars, int length, double factor)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var gain = 2 / ((double)length * length + 1); var a = R(gain); var decay = R(1 - gain); var weight = R(factor); var complement = R(1 - factor);
        var line = new ReferenceFraction[bars.Count]; var lower = new ReferenceFraction[bars.Count]; var upper = new ReferenceFraction[bars.Count]; var width = R(0); var source = R(0); var direction = 1; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var current = R(bars[i].Close); var previous = i == 0 ? current : line[i - 1]; var distance = (current - previous).Abs().RoundExtendedBinary64();
            width = (a * distance + decay * (i == 0 ? distance : width)).RoundExtendedBinary64();
            var nextSource = (weight * previous + complement * current).RoundExtendedBinary64(); var oldLower = i == 0 ? R(0) : lower[i - 1]; var oldUpper = i == 0 ? R(0) : upper[i - 1];
            var lo = (previous - width).RoundExtendedBinary64(); var hi = (previous + width).RoundExtendedBinary64();
            lower[i] = source.CompareTo(oldLower) > 0 ? new[] { lo, oldLower }.Max() : lo; upper[i] = source.CompareTo(oldUpper) < 0 ? new[] { hi, oldUpper }.Min() : hi;
            var classification = new[] { nextSource.CompareTo(oldUpper) > 0, nextSource.CompareTo(oldLower) < 0 }; if (classification[0]) direction = 1; else if (classification[1]) direction = -1;
            line[i] = direction == 1 ? upper[i] : lower[i]; var difference = line[i] - previous; var before = previous - (i < 2 ? R(0) : line[i - 2]);
            signals[i] = difference.Sign > 0 && difference.CompareTo(before) > 0 ? Signal.StrongBuy : difference.Sign < 0 && difference.CompareTo(before) < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None; source = nextSource;
        }
        return (line.Select(v => v.ToDouble()).ToArray(), signals);
    }
}
