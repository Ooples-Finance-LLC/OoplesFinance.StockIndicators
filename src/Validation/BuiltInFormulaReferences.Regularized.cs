using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RegularizedOutputs(IReadOnlyList<Bar> bars, int length, double lambda = .5)
    {
        var alpha = ReferenceFraction.FromDouble(2d / (Math.Max(1, length) + 1d)); var factor = ReferenceFraction.FromDouble(lambda);
        var denominator = ReferenceFraction.FromDouble(lambda + 1); var previous = new ReferenceFraction(0); var prior = previous;
        var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close);
            var correction = RoundRocBankStage(alpha * RoundRocBankStage(price - previous));
            var projection = RoundRocBankStage(factor * RoundRocBankStage(RoundRocBankStage(new ReferenceFraction(2) * previous) - prior));
            var numerator = RoundRocBankStage(RoundRocBankStage(previous + correction) + projection);
            var value = RoundRocBankStage(numerator / denominator);
            values[i] = value.ToDouble(); prior = previous; previous = value;
        }
        return Outputs(("Rema", values));
    }
}
