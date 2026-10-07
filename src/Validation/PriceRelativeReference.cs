using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class PriceRelativeReference
{
    internal static double?[][] Values(
        IReadOnlyList<Bar> bars,
        int? period,
        int? mean,
        CandlePriceField evaluation,
        CandlePriceField basis
    )
    {
        var e = bars.Select(b =>
                ReferenceFraction.FromDouble(PairStatisticsWindow.Select(b, evaluation))
            )
            .ToArray();
        var b = bars.Select(v =>
                ReferenceFraction.FromDouble(PairStatisticsWindow.Select(v, basis))
            )
            .ToArray();
        var r = new[] { new double?[bars.Count], new double?[bars.Count], new double?[bars.Count] };
        for (var i = 0; i < bars.Count; i++)
        {
            if (b[i].Sign != 0)
                r[0][i] = (e[i] / b[i]).ToDouble();
            if (r[0][i].HasValue && !double.IsFinite(r[0][i]!.Value))
                return r;
            if (period.HasValue && i >= period.Value)
            {
                var j = i - period.Value;
                if (e[j].Sign != 0 && b[j].Sign != 0)
                    r[2][i] = ((e[i] - e[j]) / e[j] - (b[i] - b[j]) / b[j]).ToDouble();
            }
            if (mean.HasValue && i >= mean.Value - 1)
            {
                var window = r[0].Skip(i - mean.Value + 1).Take(mean.Value).ToArray();
                if (window.All(v => v.HasValue))
                    r[1][i] = (
                        window.Aggregate(
                            new ReferenceFraction(0),
                            (s, v) => s + ReferenceFraction.FromDouble(v!.Value)
                        ) / new ReferenceFraction(mean.Value)
                    ).ToDouble();
            }
        }
        return r;
    }
}
