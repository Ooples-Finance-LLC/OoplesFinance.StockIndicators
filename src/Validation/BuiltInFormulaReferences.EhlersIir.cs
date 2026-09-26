using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EhlersIirOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = Integer(indicator.CreateOptions(), "Length", 14); var alpha = 2d / (length + 1d);
        var lag = Math.Min(530, Math.Max(2, (int)Math.Ceiling(1 / alpha - 1)));
        var previous = new ReferenceFraction(0); var zero = previous; var values = new double[bars.Count];
        for (var i = 0; i < values.Length; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close);
            var momentum = i < lag ? zero : RoundRocBankStage(price - ReferenceFraction.FromDouble(bars[i - lag].Close));
            var adjusted = RoundRocBankStage(price + momentum);
            var current = RoundRocBankStage(adjusted * ReferenceFraction.FromDouble(alpha));
            var retained = RoundRocBankStage(previous * ReferenceFraction.FromDouble(1 - alpha));
            previous = RoundRocBankStage(current + retained); values[i] = previous.ToDouble();
        }
        return Outputs(("Eiirf", values));
    }
}
