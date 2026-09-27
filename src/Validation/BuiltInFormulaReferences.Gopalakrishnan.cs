using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> GopalakrishnanOutputs(IReadOnlyList<Bar> bars, int length, int kind = 2)
    {
        length = Math.Max(2, length); var values = Enumerable.Range(0, bars.Count).Select(i =>
        {
            var subset = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).ToArray();
            var range = RoundRocBankStage(ReferenceFraction.FromDouble(subset.Max(b => b.High)) - ReferenceFraction.FromDouble(subset.Min(b => b.Low)));
            if (range.Sign <= 0) return 0;
            var published = range.ToDouble(); var decades = 0;
            while (double.IsInfinity(published)) { range /= new ReferenceFraction(10); decades++; published = range.ToDouble(); }
            return (Math.Log(published) + decades * Math.Log(10)) / Math.Log(length);
        }).ToArray();
        return new() { { "Gapo", values }, { "Signal", SmoothRocBankStage(values.Select(ReferenceFraction.FromDouble).ToArray(), length, kind).Select(v => v.ToDouble()).ToArray() } };
    }
}
