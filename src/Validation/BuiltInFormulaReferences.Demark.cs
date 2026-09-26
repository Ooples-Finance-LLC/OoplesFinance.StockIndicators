using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? DemarkPatterns(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        var options = indicator.CreateOptions();
        if (name is IndicatorName.DemarkSetupIndicator or IndicatorName.DemarkReversalPoints)
        {
            var setup = name == IndicatorName.DemarkSetupIndicator;
            var length = Integer(options, setup ? "Length" : "Length1", setup ? 4 : 9);
            var lag = setup ? length : Integer(options, "Length2", 4);
            var key = setup ? "Dsi" : "Drp";
            return new(key, new[] { key }, bars =>
            {
                double Price(int i) => i < 0 ? 0 : bars[i].Close;
                return Outputs((key, bars.Select((bar, i) =>
                {
                    var signs = Enumerable.Range(0, length)
                        .Select(j => Math.Sign(Price(i - j) - Price(i - j - lag))).Distinct().ToArray();
                    return signs.Length == 1 && signs[0] != 0 ? bar.Close : 0;
                }).ToArray()));
            });
        }
        if (name == IndicatorName.DemarkRangeExpansionIndex)
        {
            var period = Integer(options, "Length", 5);
            return new("Drei", new[] { "Drei" }, bars =>
            {
                double At(int i, Func<Bar, double> select) => i < 0 ? 0 : select(bars[i]);
                var changes = bars.Select((b, i) => b.High + b.Low - At(i - 2, v => v.High) - At(i - 2, v => v.Low)).ToArray();
                var included = bars.Select((b, i) =>
                {
                    // Preserve the library's two exclusion gates and zero-padded history.
                    var overlap = b.High >= Math.Min(At(i - 5, v => v.Low), At(i - 6, v => v.Low)) &&
                        b.Low <= Math.Max(At(i - 5, v => v.High), At(i - 6, v => v.High));
                    var previousOverlap = At(i - 2, v => v.High) >= At(i - 8, v => v.Close) &&
                        At(i - 2, v => v.Low) <= Math.Max(At(i - 7, v => v.Close), At(i - 8, v => v.Close));
                    return overlap || previousOverlap ? 0 : changes[i];
                }).ToArray();
                return Outputs(("Drei", bars.Select((_, i) =>
                {
                    var denominator = Window(changes, i, period).Sum(Math.Abs);
                    return denominator == 0 ? 0 : 100 * Window(included, i, period).Sum() / denominator;
                }).ToArray()));
            });
        }
        return null;
    }
}
