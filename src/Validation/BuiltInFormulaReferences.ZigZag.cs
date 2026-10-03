using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? ZigZagFormula(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName != IndicatorName.ZigZag) return null;
        return new("ZigZag", new[] { "ZigZag" }, bars => ZigZagOutputs(bars, indicator));
    }
    internal static IReadOnlyDictionary<string, double[]> ZigZagOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => ZigZagValues(bars, Number(indicator.CreateOptions(), 5, "Deviation"), indicator is IIndicator value && value.Source is not null);
    internal static Dictionary<string, double[]> ZigZagValues(IReadOnlyList<Bar> bars, double deviation = 5, bool selected = false)
        => ZigZagDetails(bars, deviation, selected).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) ZigZagDetails(IReadOnlyList<Bar> bars, double deviation = 5, bool selected = false)
    {
        if (double.IsNaN(deviation) || double.IsInfinity(deviation) || deviation < 0) throw new ArgumentOutOfRangeException(nameof(deviation));
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        if (bars.Count == 0) return (new() { ["ZigZag"] = Array.Empty<double>() }, Array.Empty<Signal>());
        var fraction = R(deviation) / R(100);
        var ranges = bars.Select((b, i) =>
        {
            var high = b.High; var low = b.Low;
            if (selected)
            {
                var tolerance = 1e-12 * Math.Max(Math.Abs(high), Math.Abs(low));
                if (!(b.Close >= low - tolerance && b.Close <= high + tolerance))
                { var previous = i == 0 ? b.Close : bars[i - 1].Close; high = Math.Max(b.Close, previous); low = Math.Min(b.Close, previous); }
            }
            return (High: R(high), Low: R(low));
        }).ToArray();
        var anchor = (ranges[0].High + ranges[0].Low) / R(2);
        var points = new SortedDictionary<int, ReferenceFraction> { [0] = anchor };
        var start = 1; var rising = true; var seedIndex = 0; var seed = anchor;
        while (start < bars.Count)
        {
            var reversal = -1; var extremumIndex = seedIndex; var extremum = seed;
            // Independently rescan every preceding observation in a leg.
            for (var i = start; i < bars.Count; i++)
            {
                var candidates = Enumerable.Range(start, i - start).Select(j => (Index: j, Value: rising ? ranges[j].High : ranges[j].Low))
                    .Prepend((Index: seedIndex, Value: seed));
                var extreme = rising ? candidates.OrderByDescending(p => p.Value).ThenBy(p => p.Index).First()
                    : candidates.OrderBy(p => p.Value).ThenBy(p => p.Index).First();
                extremumIndex = extreme.Index; extremum = extreme.Value;
                var opposite = rising ? ranges[i].Low : ranges[i].High; var boundary = extremum + (rising ? R(-1) : R(1)) * extremum.Abs() * fraction;
                if (rising ? opposite.CompareTo(boundary) < 0 : opposite.CompareTo(boundary) > 0) { reversal = i; break; }
            }
            if (reversal < 0)
            {
                var candidates = Enumerable.Range(start, bars.Count - start).Select(j => (Index: j, Value: rising ? ranges[j].High : ranges[j].Low))
                    .Prepend((Index: seedIndex, Value: seed));
                var extreme = rising ? candidates.OrderByDescending(p => p.Value).ThenBy(p => p.Index).First()
                    : candidates.OrderBy(p => p.Value).ThenBy(p => p.Index).First();
                points[extreme.Index] = extreme.Value; break;
            }
            points[extremumIndex] = extremum; seedIndex = reversal; seed = rising ? ranges[reversal].Low : ranges[reversal].High;
            start = reversal + 1; rising = !rising;
            if (start == bars.Count) points[seedIndex] = seed;
        }
        var result = Enumerable.Range(0, bars.Count).Select(i =>
        {
            var left = points.Last(p => p.Key <= i); var following = points.Where(p => p.Key > i).ToArray();
            if (following.Length == 0) return left.Value;
            var right = following[0];
            return ((R(right.Key - i) * left.Value + R(i - left.Key) * right.Value) / R(right.Key - left.Key));
        }).ToArray();
        var signals = new Signal[result.Length]; var prior = R(0);
        for (var i = 0; i < result.Length; i++)
        {
            var slope = result[i] - (i == 0 ? R(0) : result[i - 1]);
            signals[i] = slope.Sign > 0 && slope.CompareTo(prior) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(prior) < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None; prior = slope;
        }
        return (new() { ["ZigZag"] = result.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}
