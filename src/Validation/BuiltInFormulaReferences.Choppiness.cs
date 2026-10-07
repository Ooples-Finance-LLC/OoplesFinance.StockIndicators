using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] ChoppinessOutputs(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(2, length); var ranges = bars.Select((b, i) =>
        {
            var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var previous = ReferenceFraction.FromDouble(bars[Math.Max(0, i - 1)].Close);
            return RoundRocBankStage(new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max());
        }).ToArray();
        return Enumerable.Range(0, bars.Count).Select(i =>
        {
            var start = Math.Max(0, i - length + 1); var subset = bars.Skip(start).Take(i - start + 1).ToArray();
            var range = RoundRocBankStage(ReferenceFraction.FromDouble(subset.Max(b => b.High)) - ReferenceFraction.FromDouble(subset.Min(b => b.Low)));
            if (range.Sign <= 0) return 0;
            var sum = new ReferenceFraction(0); for (var j = start; j <= i; j++) sum += ranges[j];
            var ratio = sum / range; var published = ratio.ToDouble(); var decades = 0;
            while (double.IsInfinity(published)) { ratio /= new ReferenceFraction(10); decades++; published = ratio.ToDouble(); }
            return 100 * (Math.Log10(published) + decades) / Math.Log10(length);
        }).ToArray();
    }
}
