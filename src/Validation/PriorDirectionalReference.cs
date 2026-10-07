using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class PriorDirectionalReference
{
    private static ReferenceFraction F(double x) => ReferenceFraction.FromDouble(x);

    internal static double?[] Calculate(
        IReadOnlyList<Bar> bars,
        PriorDirectionalMeasure measure,
        int period,
        int unstable
    )
    {
        var result = new double?[bars.Count];
        var averages = new double?[bars.Count];
        var directions = new double[bars.Count];
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
        double average = 0,
            lastDx = 0;
        var start =
            period == 1 ? 1
            : measure <= PriorDirectionalMeasure.NegativeMovement ? (long)period - 1 + unstable
            : measure <= PriorDirectionalMeasure.Index ? (long)period + unstable
            : 2L * period - 1 + unstable;
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
            if (period == 1)
            {
                var movement = inputs[(int)measure % 2][i];
                result[i] =
                    measure <= PriorDirectionalMeasure.NegativeMovement ? movement.ToDouble()
                    : inputs[2][i].Sign == 0 ? 0
                    : (movement / inputs[2][i]).ToDouble();
                continue;
            }
            if (i < period - 1)
                continue;
            for (var j = 0; j < 3; j++)
                sums[j] = (
                    i == period - 1
                        ? inputs[j]
                            .Skip(1)
                            .Take(period - 1)
                            .Aggregate(new ReferenceFraction(0), (a, b) => a + b)
                        : sums[j] * new ReferenceFraction(period - 1) / divisor + inputs[j][i]
                ).RoundExtendedBinary64();
            if (measure <= PriorDirectionalMeasure.NegativeMovement)
            {
                if (i >= start)
                    result[i] = sums[(int)measure].ToDouble();
                continue;
            }
            if (i < period)
                continue;
            if (measure == PriorDirectionalMeasure.Index && i > start)
            {
                var currentRange = new[]
                {
                    F(bars[i].High) - F(bars[i].Low),
                    (F(bars[i].High) - F(bars[i].Close)).Abs(),
                    (F(bars[i].Low) - F(bars[i].Close)).Abs(),
                }
                    .Max()
                    .RoundExtendedBinary64();
                sums[2] = (
                    sums[2] * new ReferenceFraction(period - 1) / divisor + currentRange
                ).RoundExtendedBinary64();
            }
            if (
                measure
                is PriorDirectionalMeasure.PositiveIndicator
                    or PriorDirectionalMeasure.NegativeIndicator
            )
            {
                if (i >= start)
                    result[i] =
                        sums[2].Sign == 0
                            ? 0
                            : (hundred * sums[(int)measure % 2] / sums[2]).ToDouble();
                continue;
            }
            var pdi =
                sums[2].Sign == 0
                    ? new ReferenceFraction(0)
                    : (hundred * sums[0] / sums[2]).RoundExtendedBinary64();
            var mdi =
                sums[2].Sign == 0
                    ? new ReferenceFraction(0)
                    : (hundred * sums[1] / sums[2]).RoundExtendedBinary64();
            var hasDirection = (pdi + mdi).Sign != 0;
            directions[i] = hasDirection
                ? (hundred * (pdi - mdi).Abs() / (pdi + mdi)).ToDouble()
                : 0;
            if (measure == PriorDirectionalMeasure.Index)
            {
                if (i >= start)
                {
                    if (i == start || hasDirection)
                        lastDx = directions[i];
                    result[i] = lastDx;
                }
                continue;
            }
            if (i == 2L * period - 1)
                average = (
                    directions
                        .Skip(period)
                        .Take(period)
                        .Aggregate(new ReferenceFraction(0), (s, v) => s + F(v)) / divisor
                ).ToDouble();
            else if (i >= 2L * period && hasDirection)
                average = (
                    (F(average) * new ReferenceFraction(period - 1) + F(directions[i])) / divisor
                ).ToDouble();
            if (i < start)
                continue;
            averages[i] = average;
            result[i] =
                measure == PriorDirectionalMeasure.Average ? average
                : i >= period - 1 && averages[i - period + 1].HasValue
                    ? (
                        (F(average) + F(averages[i - period + 1]!.Value)) / new ReferenceFraction(2)
                    ).ToDouble()
                : null;
        }
        return result;
    }
}
