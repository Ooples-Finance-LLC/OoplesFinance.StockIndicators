using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class DeviationRatioAdaptiveReference
{
    internal static double?[] Values(IReadOnlyList<Bar> bars, DeviationRatioAdaptiveAverage owner)
    {
        var result = new double?[bars.Count];
        var previous = new ReferenceFraction(0);
        var alpha = ReferenceFraction.FromDouble(owner.Alpha);
        for (var i = 0; i < bars.Count; i++)
        {
            var shortValues = bars.Skip(Math.Max(0, i - owner.ShortPeriod + 1))
                .Take(Math.Min(i + 1, owner.ShortPeriod))
                .Select(b => ReferenceFraction.FromDouble(b.Close))
                .ToArray();
            var mean =
                shortValues.Aggregate(new ReferenceFraction(0), (a, b) => a + b)
                / new ReferenceFraction(shortValues.Length);
            if (i < owner.LongPeriod)
                previous = mean.RoundExtendedBinary64();
            else
            {
                var longValues = bars.Skip(Math.Max(0, i - owner.LongPeriod + 1))
                    .Take(Math.Min(i + 1, owner.LongPeriod))
                    .Select(b => ReferenceFraction.FromDouble(b.Close))
                    .ToArray();
                var longMean =
                    longValues.Aggregate(new ReferenceFraction(0), (a, b) => a + b)
                    / new ReferenceFraction(longValues.Length);
                var longVariance =
                    longValues.Aggregate(
                        new ReferenceFraction(0),
                        (a, b) => a + (b - longMean) * (b - longMean)
                    ) / new ReferenceFraction(longValues.Length);
                if (longVariance.Sign == 0)
                    break;
                var shortVariance =
                    shortValues.Aggregate(
                        new ReferenceFraction(0),
                        (a, b) => a + (b - mean) * (b - mean)
                    ) / new ReferenceFraction(shortValues.Length);
                var squaredRatio = shortVariance / longVariance;
                var scale = new ReferenceFraction(1);
                var power = new ReferenceFraction(System.Numerics.BigInteger.One << 512);
                var root = squaredRatio.SqrtToDouble();
                while (double.IsInfinity(root))
                {
                    squaredRatio /= power * power;
                    scale *= power;
                    root = squaredRatio.SqrtToDouble();
                }
                var ratio = ReferenceFraction.FromDouble(root) * scale;
                var gain = (alpha * ratio).RoundExtendedBinary64();
                previous = (
                    gain * ReferenceFraction.FromDouble(bars[i].Close)
                    + (new ReferenceFraction(1) - gain) * previous
                ).RoundExtendedBinary64();
            }
            result[i] = previous.ToDouble();
        }
        return result;
    }
}
