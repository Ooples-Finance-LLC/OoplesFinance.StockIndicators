using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> DetrendedLeadingOutputs(IReadOnlyList<Bar> bars, int length = 14)
    {
        var alpha = length > 2 ? 2d / (length + 1d) : .67; var half = alpha / 2;
        var fast = new ReferenceFraction(0); var slow = fast; var signal = fast;
        var dsp = new double[bars.Count]; var deli = new double[bars.Count];
        ReferenceFraction Blend(ReferenceFraction v, ReferenceFraction p, double a) => RoundRocBankStage(RoundRocBankStage(v * ReferenceFraction.FromDouble(a)) + RoundRocBankStage(p * ReferenceFraction.FromDouble(1 - a)));
        for (var i = 0; i < bars.Count; i++)
        {
            var high = Math.Max(i == 0 ? 0 : bars[i - 1].High, bars[i].High); var low = Math.Min(i == 0 ? 0 : bars[i - 1].Low, bars[i].Low);
            var midpoint = ReferenceFraction.FromDouble(((ReferenceFraction.FromDouble(high) + ReferenceFraction.FromDouble(low)) / new ReferenceFraction(2)).ToDouble());
            if (i == 0) fast = slow = midpoint;
            fast = Blend(midpoint, fast, alpha); slow = Blend(midpoint, slow, half); var difference = RoundRocBankStage(fast - slow);
            signal = Blend(difference, signal, alpha); dsp[i] = difference.ToDouble(); deli[i] = RoundRocBankStage(difference - signal).ToDouble();
        }
        return new() { { "Dsp", dsp }, { "Deli", deli } };
    }
}
