using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedStandardError(IReadOnlyList<Bar> bars, int length, bool regression)
        => bars.Select((_, i) =>
        {
            if (i + 1 < length) return 0d;
            var n = new ReferenceFraction(length);
            var values = bars.Skip(i - length + 1).Take(length).Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
            var mean = values.Aggregate(new ReferenceFraction(0), (sum, v) => sum + v) / n;
            var center = new ReferenceFraction(length - 1) / new ReferenceFraction(2);
            var xx = new ReferenceFraction(0); var xy = new ReferenceFraction(0);
            if (regression)
                for (var j = 0; j < length; j++)
                {
                    var position = new ReferenceFraction(j) - center;
                    xx += position * position;
                    xy += position * (values[j] - mean);
                }
            var slope = regression && length > 1 ? xy / xx : new ReferenceFraction(0);
            var squared = new ReferenceFraction(0);
            for (var j = 0; j < length; j++)
            {
                var residual = values[j] - mean - slope * (new ReferenceFraction(j) - center);
                squared += residual * residual;
            }
            return (squared / (regression ? n : n * n)).SqrtToDouble();
        }).ToArray();
}
