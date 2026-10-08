using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) ZeroLagCycleValues(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); var smooth = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d)));
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var line = bars.Select(b => R(b.Close)).ToArray();
        for (var stage = 0; stage < 6; stage++)
        {
            var next = new ReferenceFraction[line.Length];
            for (var i = 0; i < line.Length; i++)
            {
                var first = Math.Max(0, i - length + 1); var count = i - first + 1; var meanX = R((count - 1) / 2d); var sum = R(0); var covariance = R(0); var squares = R(0);
                for (var j = first; j <= i; j++) { var x = R(j - first) - meanX; sum += line[j]; covariance += x * line[j]; squares += x * x; }
                var fit = RoundRocBankStage(sum / R(count) + (count == 1 ? R(0) : covariance / squares * meanX));
                next[i] = RoundRocBankStage((stage % 2 == 0 ? R(1) : R(2)) * line[i] - fit);
            }
            line = next;
        }
        ReferenceFraction[] Mean(ReferenceFraction[] input)
        {
            var result = new ReferenceFraction[input.Length];
            for (var i = 0; i < input.Length; i++) { var first = Math.Max(0, i - smooth + 1); var sum = R(0); for (var j = first; j <= i; j++) sum += input[j]; result[i] = RoundRocBankStage(sum / R(i - first + 1)); }
            return result;
        }
        var filter = Mean(Mean(line)).Select(v => R(-2) * v).ToArray(); var signals = new Signal[bars.Count]; var previous = R(0);
        for (var i = 0; i < bars.Count; i++) { var difference = line[i] - filter[i]; var increasing = difference.CompareTo(previous); signals[i] = difference.Sign > 0 && increasing > 0 ? Signal.StrongBuy : difference.Sign < 0 && increasing < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None; previous = difference; }
        return (new Dictionary<string, double[]> { { "Lco", line.Select(v => v.ToDouble()).ToArray() }, { "Filter", filter.Select(v => v.ToDouble()).ToArray() } }, signals);
    }
}
