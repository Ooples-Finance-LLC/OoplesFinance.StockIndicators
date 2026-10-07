using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HampelOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => HampelValues(bars, Integer(indicator.CreateOptions(), "Length", 14), Number(indicator.CreateOptions(), 3, "ScalingFactor"));
    internal static Dictionary<string, double[]> HampelValues(IReadOnlyList<Bar> bars, int length = 14, double factor = 3)
    {
        length = Math.Max(1, length); var scale = ReferenceFraction.FromDouble(factor);
        ReferenceFraction Median(IEnumerable<ReferenceFraction> values)
        {
            var ordered = values.OrderBy(v => v).ToArray();
            return (ordered[(ordered.Length - 1) / 2] + ordered[ordered.Length / 2]) / new ReferenceFraction(2);
        }
        var values = new double[bars.Count]; var previous = new ReferenceFraction(0);
        var alpha = new ReferenceFraction(2) / new ReferenceFraction(length + 1L);
        for (var i = 0; i < bars.Count; i++)
        {
            var current = ReferenceFraction.FromDouble(bars[i].Close);
            var history = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
            var median = Median(history); var deviation = Median(history.Select(v => (v - median).Abs()));
            var accepted = (current - median).Abs().CompareTo(scale * deviation) <= 0;
            var filtered = accepted ? current : median;
            previous += alpha * (filtered - previous); values[i] = previous.ToDouble();
        }
        return new() { ["Hf"] = values };
    }
}
