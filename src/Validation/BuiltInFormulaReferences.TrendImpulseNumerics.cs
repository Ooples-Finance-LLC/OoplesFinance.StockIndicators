using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedTrendImpulseReference(IReadOnlyList<Bar> bars, object options)
    {
        var values = Closes(bars);
        var period = Math.Max(2, Integer(options, "Length1", 100));
        var events = Enumerable.Range(0, values.Length).Where(i => i == 0 ||
            values[i] > Window(values, i - 1, period).Max() || values[i] < Window(values, i - 1, period).Min()).ToArray();
        var held = values.Select((_, i) => values[events.Last(j => j <= i)]).ToArray();
        return RoundedBoundedStage(held, Integer(options, "Length2", 10), BoundedMeanKind(options, 2));
    }
}
