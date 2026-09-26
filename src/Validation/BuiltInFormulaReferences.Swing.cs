using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SwingOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return SwingOutputs(bars, indicator.BatchName == IndicatorName.AccumulativeSwingIndex, Math.Max(1, Integer(options, "Length", 14)), AverageKind(options, 1), Number(options, 0, "LimitMove"));
    }
    internal static IReadOnlyDictionary<string, double[]> SwingOutputs(IReadOnlyList<Bar> bars, bool cumulative, int length, int kind, double limit)
    {
        var zero = new ReferenceFraction(0); var four = new ReferenceFraction(4); var two = new ReferenceFraction(2);
        ReferenceFraction Abs(ReferenceFraction v) => v.Sign < 0 ? zero - v : v;
        var values = new ReferenceFraction[bars.Count]; var total = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var swing = zero;
            if (i > 0)
            {
                var b = bars[i]; var previous = bars[i - 1];
                var c = ReferenceFraction.FromDouble(b.Close); var o = ReferenceFraction.FromDouble(b.Open);
                var pc = ReferenceFraction.FromDouble(previous.Close); var po = ReferenceFraction.FromDouble(previous.Open);
                var h = ReferenceFraction.FromDouble(b.High); var l = ReferenceFraction.FromDouble(b.Low);
                var a = Abs(h - pc); var d = Abs(l - pc); var large = (a - d).Sign >= 0 ? a : d; var small = (a - d).Sign >= 0 ? d : a;
                var range = h - l; var divisor = ((large - range).Sign >= 0 ? large - small / two : range) + Abs(pc - po) / four;
                var scale = limit > 0 ? ReferenceFraction.FromDouble(limit) : range;
                var movement = c - pc + (c - o) / two + (pc - po) / four;
                if (divisor.Sign != 0 && scale.Sign != 0) swing = (new ReferenceFraction(50) * large * movement / (divisor * scale)).RoundExtendedBinary64();
            }
            total = (total + swing).RoundExtendedBinary64(); values[i] = cumulative ? total : swing;
        }
        if (!cumulative) return Outputs(("Si", values.Select(v => v.ToDouble()).ToArray()));
        return Outputs(("Asi", values.Select(v => v.ToDouble()).ToArray()), ("Signal", SmoothRocBankStage(values, length, kind).Select(v => v.ToDouble()).ToArray()));
    }
}
