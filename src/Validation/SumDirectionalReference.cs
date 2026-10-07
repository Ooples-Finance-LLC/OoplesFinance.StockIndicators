using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class SumDirectionalReference
{
    private static ReferenceFraction F(double value) => ReferenceFraction.FromDouble(value);

    internal static double?[][] Calculate(IReadOnlyList<Bar> bars, int period)
    {
        var result = Enumerable.Range(0, 5).Select(_ => new double?[bars.Count]).ToArray();
        var inputs = Enumerable
            .Range(0, 3)
            .Select(_ => new ReferenceFraction[bars.Count])
            .ToArray();
        var sums = new[]
        {
            new ReferenceFraction(0),
            new ReferenceFraction(0),
            new ReferenceFraction(0),
        };
        var divisor = new ReferenceFraction(period);
        var hundred = new ReferenceFraction(100);
        double? previousAverage = null;
        for (var i = 1; i < bars.Count; i++)
        {
            var up = F(bars[i].High) - F(bars[i - 1].High);
            var down = F(bars[i - 1].Low) - F(bars[i].Low);
            inputs[0][i] = (
                up.Sign > 0 && up.CompareTo(down) > 0 ? up : new ReferenceFraction(0)
            ).RoundExtendedBinary64();
            inputs[1][i] = (
                down.Sign > 0 && down.CompareTo(up) > 0 ? down : new ReferenceFraction(0)
            ).RoundExtendedBinary64();
            inputs[2][i] = new[]
            {
                F(bars[i].High) - F(bars[i].Low),
                (F(bars[i].High) - F(bars[i - 1].Close)).Abs(),
                (F(bars[i].Low) - F(bars[i - 1].Close)).Abs(),
            }
                .Max()
                .RoundExtendedBinary64();
            if (i < period)
                continue;
            for (var j = 0; j < 3; j++)
                sums[j] = (
                    i == period
                        ? inputs[j]
                            .Skip(1)
                            .Take(period)
                            .Aggregate(new ReferenceFraction(0), (a, b) => a + b)
                        : sums[j] * new ReferenceFraction(period - 1) / divisor + inputs[j][i]
                ).RoundExtendedBinary64();
            if (sums[2].Sign == 0)
                continue;
            var pdi = (hundred * sums[0] / sums[2]).RoundExtendedBinary64();
            var mdi = (hundred * sums[1] / sums[2]).RoundExtendedBinary64();
            result[0][i] = pdi.ToDouble();
            result[1][i] = mdi.ToDouble();
            result[2][i] =
                (pdi + mdi).Sign == 0 ? 0 : (hundred * (pdi - mdi).Abs() / (pdi + mdi)).ToDouble();
            if (i == 2L * period - 1)
                previousAverage = (
                    result[2]
                        .Skip(period)
                        .Take(period)
                        .Aggregate(new ReferenceFraction(0), (sum, v) => sum + F(v ?? 0)) / divisor
                ).ToDouble();
            else if (i >= 2L * period && previousAverage.HasValue)
                previousAverage = (
                    (
                        F(previousAverage.Value) * new ReferenceFraction(period - 1)
                        + F(result[2][i]!.Value)
                    ) / divisor
                ).ToDouble();
            result[3][i] = previousAverage;
            if (i >= period && previousAverage.HasValue && result[3][i - period].HasValue)
                result[4][i] = (
                    (F(previousAverage.Value) + F(result[3][i - period]!.Value))
                    / new ReferenceFraction(2)
                ).ToDouble();
        }
        return result;
    }
}
