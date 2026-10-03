using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TrendTriggerOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = Integer(indicator.CreateOptions(), "Length", 15); var zero = new ReferenceFraction(0);
        var peaks = bars.Select((_, i) => bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).Max(b => b.High)).ToArray();
        var troughs = bars.Select((_, i) => bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).Min(b => b.Low)).ToArray();
        var output = bars.Select((_, i) =>
        {
            var buy = ReferenceFraction.FromDouble(peaks[i]) - (i < length ? zero : ReferenceFraction.FromDouble(troughs[i - length]));
            var sell = (i < length ? zero : ReferenceFraction.FromDouble(peaks[i - length])) - ReferenceFraction.FromDouble(troughs[i]);
            return (buy + sell).Sign == 0 ? 0 : (new ReferenceFraction(200) * (buy - sell) / (buy + sell)).ToDouble();
        }).ToArray();
        return Outputs(("Ttf", output));
    }
}
