using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class RangeAccelerationReference
{
    internal static double?[][] Values(IReadOnlyList<Bar> bars, int period)
    {
        var rows = Enumerable.Range(0, 3).Select(_ => new ReferenceFraction[bars.Count]).ToArray();
        var result = Enumerable.Range(0, 3).Select(_ => new double?[bars.Count]).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            var h = ReferenceFraction.FromDouble(bars[i].High);
            var l = ReferenceFraction.FromDouble(bars[i].Low);
            var acceleration =
                (h + l).Sign == 0
                    ? new ReferenceFraction(0)
                    : new ReferenceFraction(4) * (h - l) / (h + l);
            rows[0][i] = (h * (new ReferenceFraction(1) + acceleration)).RoundExtendedBinary64();
            rows[1][i] = ReferenceFraction.FromDouble(bars[i].Close);
            rows[2][i] = (l * (new ReferenceFraction(1) - acceleration)).RoundExtendedBinary64();
            if (i < period - 1)
                continue;
            for (var j = 0; j < 3; j++)
                result[j][i] = (
                    rows[j]
                        .Skip(i - period + 1)
                        .Take(period)
                        .Aggregate(new ReferenceFraction(0), (s, v) => s + v)
                    / new ReferenceFraction(period)
                ).ToDouble();
        }
        return result;
    }
}
