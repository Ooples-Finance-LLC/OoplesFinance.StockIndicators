using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedAwesomeReference(IReadOnlyList<Bar> bars, object options,
        bool accelerator, IReadOnlyList<double>? selected = null)
    {
        var kind = BoundedMeanKind(options, 1);
        var values = selected?.ToArray() ?? bars.Select(b => ((ReferenceFraction.FromDouble(b.High)
            + ReferenceFraction.FromDouble(b.Low)) / new ReferenceFraction(2)).ToDouble()).ToArray();
        var fast = RoundedBoundedStage(values, Integer(options, "FastLength", Integer(options, "Length", 5)), kind);
        var slow = RoundedBoundedStage(values, Integer(options, "SlowLength", 34), kind);
        var awesome = fast.Select((v, i) => (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(slow[i])).ToDouble()).ToArray();
        if (!accelerator) return awesome;
        var count = awesome.TakeWhile(v => !double.IsInfinity(v) && !double.IsNaN(v)).Count();
        var signal = RoundedBoundedStage(awesome.Take(count).ToArray(), Integer(options, "SmoothLength", 5), kind);
        return awesome.Select((v, i) => i < count ? (ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(signal[i])).ToDouble() : double.NaN).ToArray();
    }
}
