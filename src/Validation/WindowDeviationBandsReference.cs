using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class WindowDeviationBandsReference
{
    internal static double?[][] Values(IReadOnlyList<Bar> bars, WindowDeviationBands owner)
    {
        var result = Enumerable.Range(0, 6).Select(_ => new double?[bars.Count]).ToArray();
        for (var i = owner.Period - 1; i < bars.Count; i++)
        {
            var window = bars.Skip(i - owner.Period + 1)
                .Take(owner.Period)
                .Select(b => ReferenceFraction.FromDouble(b.Close))
                .ToArray();
            var exactMean =
                window.Aggregate(new ReferenceFraction(0), (a, b) => a + b)
                / new ReferenceFraction(owner.Period);
            var variance =
                window.Aggregate(
                    new ReferenceFraction(0),
                    (a, b) => a + (b - exactMean) * (b - exactMean)
                ) / new ReferenceFraction(owner.Period);
            var center = ReferenceFraction.FromDouble(exactMean.ToDouble());
            var deviation = ReferenceFraction.FromDouble(variance.SqrtToDouble());
            var half = (
                deviation * ReferenceFraction.FromDouble(owner.Factor)
            ).RoundExtendedBinary64();
            result[0][i] = center.ToDouble();
            result[1][i] = (center + half).ToDouble();
            result[2][i] = (center - half).ToDouble();
            if (half.Sign != 0)
                result[3][i] = (
                    (window[window.Length - 1] - center + half) / (new ReferenceFraction(2) * half)
                ).ToDouble();
            if (variance.Sign > 0)
            {
                var delta = window[window.Length - 1] - exactMean;
                result[4][i] = delta.Sign * (delta * delta / variance).SqrtToDouble();
            }
            if (center.Sign != 0)
                result[5][i] = (
                    new ReferenceFraction(owner.WidthAsPercent ? 200 : 2) * half / center
                ).ToDouble();
        }
        return result;
    }
}
