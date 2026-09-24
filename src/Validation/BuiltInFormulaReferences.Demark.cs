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
        if (name == IndicatorName.DemarkPressureRatioV1)
        {
            var period = Integer(options, "Length", 13);
            return new("Dpr", new[] { "Dpr" }, bars =>
            {
                var buy = new double[bars.Count];
                var sell = new double[bars.Count];
                for (var i = 0; i < bars.Count; i++)
                {
                    var b = bars[i];
                    var previous = i == 0 ? 0 : bars[i - 1].Close;
                    var upGap = previous == 0 ? 0 : (b.Open - previous) / previous;
                    var downGap = b.Open == 0 ? 0 : (previous - b.Open) / b.Open;
                    buy[i] = b.Volume * (upGap > .15 ? b.High - previous + b.Close - b.Low : Math.Max(0, b.Close - b.Open));
                    sell[i] = b.Volume * (downGap > .15 ? previous - b.Low + b.High - b.Close : Math.Max(0, b.Open - b.Close));
                }
                return Outputs(("Dpr", bars.Select((_, i) =>
                {
                    var buyers = Window(buy, i, period).Sum();
                    var total = buyers + Window(sell, i, period).Sum();
                    return total == 0 ? 0 : Math.Max(0, Math.Min(100, 100 * buyers / total));
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
