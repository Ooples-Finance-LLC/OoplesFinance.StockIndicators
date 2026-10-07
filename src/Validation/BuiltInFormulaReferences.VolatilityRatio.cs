using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] VolatilityRatioOutputs(IReadOnlyList<Bar> bars, int length = 14)
    {
        length = Math.Max(1, length); var rangeLength = Math.Max(1, length - 1); var result = new double[bars.Count];
        ReferenceFraction Difference(double a, double b) => RoundRocBankStage(ReferenceFraction.FromDouble(a) - ReferenceFraction.FromDouble(b));
        ReferenceFraction Abs(ReferenceFraction v) => v.Sign < 0 ? new ReferenceFraction(0) - v : v;
        ReferenceFraction Max(ReferenceFraction a, ReferenceFraction b) => (a - b).Sign >= 0 ? a : b;
        for (var i = 0; i < bars.Count; i++)
        {
            if (i == 0) continue;
            var priorBars = bars.Skip(Math.Max(0, i - rangeLength)).Take(Math.Min(i, rangeLength)).ToArray();
            var high = priorBars.Max(b => b.High); var low = priorBars.Min(b => b.Low);
            var prior = i >= length + 1L ? bars[i - length - 1].Close : 0;
            if (prior != 0) { high = Math.Max(high, prior); low = Math.Min(low, prior); }
            var bar = bars[i]; var previous = bars[i - 1].Close;
            var range = Max(Difference(bar.High, bar.Low), Max(Abs(Difference(bar.High, previous)), Abs(Difference(bar.Low, previous))));
            var denominator = Difference(high, low); result[i] = denominator.Sign == 0 ? 0 : (range / denominator).ToDouble();
        }
        return result;
    }
}
