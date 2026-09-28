using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] PercentageTrendValues(IReadOnlyList<Bar> bars, int length, double percentage)
    {
        length = Math.Max(1, length);
        var up = ReferenceFraction.FromDouble(1 + percentage); var down = ReferenceFraction.FromDouble(1 - percentage);
        double At(int index) => index < 0 ? 0 : bars[index].Close;
        return bars.Select((bar, index) =>
        {
            var line = bar.Close; var span = 0;
            for (var lag = 1; lag <= length; lag++)
            {
                var older = At(index - lag); var newer = At(index - lag + 1);
                if (newer <= line && older > line || newer >= line && older < line) span = 0;
                var values = Enumerable.Range(index - span, span + 1).Select(At).Append(older);
                // Before each iteration span <= lag-1 < length, so the legacy fallback branch is unreachable.
                line = older > line ? (ReferenceFraction.FromDouble(values.Max()) * down).ToDouble()
                    : (ReferenceFraction.FromDouble(values.Min()) * up).ToDouble();
                span++;
            }
            return line;
        }).ToArray();
    }
}
