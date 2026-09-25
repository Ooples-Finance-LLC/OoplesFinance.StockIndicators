using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedTrendForceReference(IReadOnlyList<Bar> bars, int length)
    {
        var values = Closes(bars);
        var period = Math.Max(2, length);
        var up = values.Select((v, i) => v > (i == 0 ? 0 : Window(values, i - 1, period).Max())).ToArray();
        var down = values.Select((v, i) => v < (i == 0 ? 0 : Window(values, i - 1, period).Min())).ToArray();
        int EventsAfterReset(bool[] events, bool[] opposite, int i)
        {
            var reset = Enumerable.Range(0, i + 1).Where(j => opposite[j] && (j == 0 || !opposite[j - 1])).DefaultIfEmpty(-1).Max();
            return Enumerable.Range(reset + 1, i - reset).Count(j => events[j]);
        }
        var counts = values.Select((_, i) => new ReferenceFraction(EventsAfterReset(up, down, i) + EventsAfterReset(down, up, i)) / new ReferenceFraction(2)).ToArray();
        return counts.Select((count, i) =>
        {
            var sum = new ReferenceFraction(0);
            for (var j = 0; j <= i; j++) sum += counts[j];
            var mean = ReferenceFraction.FromDouble((sum / new ReferenceFraction(i + 1)).ToDouble());
            return (count - mean).ToDouble();
        }).ToArray();
    }
}
