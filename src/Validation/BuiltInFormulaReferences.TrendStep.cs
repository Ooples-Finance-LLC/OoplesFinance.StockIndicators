using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] TrendStepOutputs(IReadOnlyList<Bar> bars, int length = 50)
    {
        length = Math.Max(1, length); var prices = Closes(bars); var deviations = PopulationDeviation(prices, length); var result = new double[bars.Count]; var previous = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(prices[i]); var twice = RoundRocBankStage(ReferenceFraction.FromDouble(deviations[i]) * new ReferenceFraction(2));
            var upper = RoundRocBankStage(previous + twice); var lower = RoundRocBankStage(previous - twice);
            if (i < length || price.CompareTo(upper) > 0 || price.CompareTo(lower) < 0) previous = price;
            result[i] = previous.ToDouble();
        }
        return result;
    }
}
