using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget ModifiedGannBudget = new(0, 4e-15, requireSameSign: true);
    internal static Dictionary<string, double[]> ModifiedGannOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return ModifiedGannValues(bars, Integer(options, "Length", 10), AverageKind(options, 1), 1).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) ModifiedGannValues(IReadOnlyList<Bar> bars, int length, int kind, double mult,
        IReadOnlyList<double>[]? external = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var zero = R(0); var upper = new ReferenceFraction[bars.Count]; var lower = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var sample = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).ToArray();
            // Expand the affine combination independently of production's difference form.
            upper[i] = (R(1) - R(mult)) * R(Math.Max(bars[i].Open, bars[i].Close)) + R(mult) * R(sample.Max(b => b.High));
            lower[i] = (R(1) - R(mult)) * R(Math.Min(bars[i].Open, bars[i].Close)) + R(mult) * R(sample.Min(b => b.Low));
        }
        ReferenceFraction[] Mean(ReferenceFraction[] values, int slot)
            => external is not null ? Enumerable.Range(0, bars.Count).Select(i => i < external[slot].Count ? R(external[slot][i]) : zero).ToArray()
            : kind is 1 or 2 or 3 or 6 ? SmoothRocBankStage(values, length, kind, v => v)
            : Average(values.Select(v => v.ToDouble()).ToArray(), length, kind).Select(R).ToArray();
        var e = Mean(upper, 0); var f = Mean(lower, 1);
        var prices = bars.Select(b => R(b.Close)).ToArray();
        // Reconstruct the last switching event rather than share the state recurrence.
        var events = Enumerable.Range(0, bars.Count).Where(i => prices[i].CompareTo(e[i]) > 0 || prices[i].CompareTo(f[i]) > 0).ToArray();
        var output = new double[bars.Count]; var signals = new Signal[bars.Count]; var oldMargin = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var last = events.Where(j => j <= i).DefaultIfEmpty(-1).Last();
            var line = last >= 0 && prices[last].CompareTo(e[last]) > 0 ? f[i] : e[i];
            output[i] = line.ToDouble(); var margin = prices[i] - line; var change = margin.CompareTo(oldMargin);
            signals[i] = margin.Sign > 0 && change > 0 ? Signal.StrongBuy : margin.Sign < 0 && change < 0 ? Signal.StrongSell
                : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
            oldMargin = margin;
        }
        return (new Dictionary<string, double[]> { ["Ghla"] = output }, signals);
    }
}
