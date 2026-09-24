using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? OptimizedTrendFormula(IBuiltInIndicator indicator)
    {
        if (indicator.CreateOptions() is not OptimizedTrendTrackerSpecOptions options) return null;
        var kind = AverageKind(options, 0);
        if (kind == 0 && options.MaType != MovingAvgType.VariableIndexDynamicAverage) return null;
        return new("Ott", new[] { "Ott" }, bars =>
        {
            var prices = Closes(bars);
            double[] average;
            if (options.MaType == MovingAvgType.VariableIndexDynamicAverage)
            {
                average = RoundedVidya(bars, options.Length);
            }
            else average = Average(prices, options.Length, kind);
            var result = new double[bars.Count];
            var lowerCandidates = new List<double>(); var upperCandidates = new List<double>();
            var events = new List<(int Index, bool Rising)> { (-1, true) };
            for (var i = 0; i < bars.Count; i++)
            {
                var value = average[i];
                if (i > 0)
                {
                    var lower = lowerCandidates.Max(); var upper = upperCandidates.Min();
                    if (events.Last().Rising && value < lower) events.Add((i, false));
                    else if (!events.Last().Rising && value > upper) events.Add((i, true));
                    if (value <= lower) lowerCandidates.Clear();
                    if (value >= upper) upperCandidates.Clear();
                }
                var distance = Math.Abs(value)*options.Percent/100;
                lowerCandidates.Add(value-distance); upperCandidates.Add(value+distance);
                var stop = events.Last().Rising ? lowerCandidates.Max() : upperCandidates.Min();
                result[i] = stop*(1 + (value > stop ? 1 : -1)*options.Percent/200);
            }
            return Outputs(("Ott", result));
        });
    }
}
