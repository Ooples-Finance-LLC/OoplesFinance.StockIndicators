using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? GuppyCountBackFormula(IBuiltInIndicator indicator)
    {
        if (indicator.CreateOptions() is not GuppyCountBackLineSpecOptions options) return null;
        return new("Cbl", new[] { "Cbl" }, bars =>
        {
            var output = bars.Select((bar, i) =>
            {
                var indices = Enumerable.Range(Math.Max(0, i-options.Length+1), Math.Min(i+1, options.Length)).Reverse().ToArray();
                var top = indices.OrderByDescending(j => bars[j].High).First();
                var bottom = indices.OrderBy(j => bars[j].Low).First();
                var rising = top >= bottom; var pivot = Math.Max(top, bottom);
                double Level(int j) => rising ? bars[j].Low : -bars[j].High;
                var history = Enumerable.Range(Math.Max(0, pivot-options.Length), Math.Min(pivot, options.Length)).Reverse().ToArray();
                // Record minima of low (or negative high), starting at the chosen pivot.
                var records = history.Where(j => Level(j) < Enumerable.Range(j+1, pivot-j).Min(Level)).ToArray();
                return records.Length < 2 ? bar.Close : rising ? bars[records[1]].Low : bars[records[1]].High;
            }).ToArray();
            return Outputs(("Cbl", output));
        });
    }
}
