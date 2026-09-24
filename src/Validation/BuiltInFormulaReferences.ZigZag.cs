using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? ZigZagFormula(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName != IndicatorName.ZigZag) return null;
        var fraction = Number(indicator.CreateOptions(), 5, "Deviation")/100;
        return new("ZigZag", new[] { "ZigZag" }, bars =>
        {
            if (bars.Count == 0) return Outputs(("ZigZag", Array.Empty<double>()));
            var anchor = (bars[0].High+bars[0].Low)/2;
            var points = new SortedDictionary<int,double> { [0] = anchor };
            var start = 1; var rising = true; var seedIndex = 0; var seed = anchor;
            while (start < bars.Count)
            {
                var reversal = -1; var extremumIndex = seedIndex; var extremum = seed;
                // Each candidate reversal is measured against the entire preceding leg,
                // independently recomputing its extremum from its observations.
                for (var i = start; i < bars.Count; i++)
                {
                    var candidates = Enumerable.Range(start, i-start)
                        .Select(j => (Index: j, Value: rising ? bars[j].High : bars[j].Low))
                        .Prepend((Index: seedIndex, Value: seed));
                    var extreme = rising ? candidates.OrderByDescending(p => p.Value).ThenBy(p => p.Index).First()
                        : candidates.OrderBy(p => p.Value).ThenBy(p => p.Index).First();
                    extremumIndex = extreme.Index; extremum = extreme.Value;
                    var opposite = rising ? bars[i].Low : bars[i].High;
                    if (rising ? opposite < extremum-Math.Abs(extremum)*fraction : opposite > extremum+Math.Abs(extremum)*fraction)
                    { reversal = i; break; }
                }
                if (reversal < 0)
                {
                    var candidates = Enumerable.Range(start, bars.Count-start)
                        .Select(j => (Index: j, Value: rising ? bars[j].High : bars[j].Low))
                        .Prepend((Index: seedIndex, Value: seed));
                    var extreme = rising ? candidates.OrderByDescending(p => p.Value).ThenBy(p => p.Index).First()
                        : candidates.OrderBy(p => p.Value).ThenBy(p => p.Index).First();
                    points[extreme.Index] = extreme.Value;
                    break;
                }
                points[extremumIndex] = extremum;
                seedIndex = reversal; seed = rising ? bars[reversal].Low : bars[reversal].High;
                start = reversal+1; rising = !rising;
                if (start == bars.Count) points[seedIndex] = seed;
            }
            var result = Enumerable.Range(0, bars.Count).Select(i =>
            {
                var left = points.Last(p => p.Key <= i);
                var following = points.Where(p => p.Key > i).ToArray();
                if (following.Length == 0) return left.Value;
                var right = following[0];
                return ((right.Key-i)*left.Value+(i-left.Key)*right.Value)/(right.Key-left.Key);
            }).ToArray();
            return Outputs(("ZigZag", result));
        });
    }
}
