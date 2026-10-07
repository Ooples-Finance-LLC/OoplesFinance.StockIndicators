using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class ReturnBetaReference
{
    internal static double?[][] Calculate(
        IReadOnlyList<Bar> bars,
        int period,
        CandlePriceField market,
        CandlePriceField evaluation,
        ReturnBetaSelection selection,
        bool full
    )
    {
        var result = Enumerable
            .Range(0, full ? 7 : 1)
            .Select(_ => new double?[bars.Count])
            .ToArray();
        double Field(Bar b, CandlePriceField f) =>
            f switch
            {
                CandlePriceField.Open => b.Open,
                CandlePriceField.High => b.High,
                CandlePriceField.Low => b.Low,
                _ => b.Close,
            };
        var x = new ReferenceFraction[bars.Count];
        var y = new ReferenceFraction[bars.Count];
        ReferenceFraction Change(double current, double previous) =>
            previous == 0
                ? new ReferenceFraction(0)
                : (
                    (ReferenceFraction.FromDouble(current) - ReferenceFraction.FromDouble(previous))
                    / ReferenceFraction.FromDouble(previous)
                ).RoundExtendedBinary64();
        for (var i = 0; i < bars.Count; i++)
        {
            x[i] = Change(Field(bars[i], market), i == 0 ? 0 : Field(bars[i - 1], market));
            y[i] = Change(Field(bars[i], evaluation), i == 0 ? 0 : Field(bars[i - 1], evaluation));
            if (full)
            {
                result[5][i] = y[i].ToDouble();
                result[6][i] = x[i].ToDouble();
            }
            if (i < period)
                continue;
            for (var slot = 0; slot < (full ? 3 : 1); slot++)
            {
                if (selection != ReturnBetaSelection.All && (int)selection != slot)
                    continue;
                var indices = Enumerable
                    .Range(i - period + 1, period)
                    .Where(j =>
                        slot == 0 || slot == 1 && x[j].Sign > 0 || slot == 2 && x[j].Sign < 0
                    )
                    .ToArray();
                if (indices.Length == 0)
                {
                    if (!full)
                        result[0][i] = 0;
                    continue;
                }
                var n = new ReferenceFraction(indices.Length);
                var mx = indices.Aggregate(new ReferenceFraction(0), (s, j) => s + x[j]) / n;
                var my = indices.Aggregate(new ReferenceFraction(0), (s, j) => s + y[j]) / n;
                var a = new ReferenceFraction(0);
                var c = a;
                foreach (var j in indices)
                {
                    var dx = x[j] - mx;
                    a += dx * dx;
                    c += dx * (y[j] - my);
                }
                result[slot][i] =
                    a.Sign == 0
                        ? full
                            ? null
                            : 0
                        : (c / a).ToDouble();
            }
            if (
                !full
                || selection != ReturnBetaSelection.All
                || !result[1][i].HasValue
                || !result[2][i].HasValue
                || !double.IsFinite(result[1][i]!.Value)
                || !double.IsFinite(result[2][i]!.Value)
            )
                continue;
            var up = ReferenceFraction.FromDouble(result[1][i]!.Value);
            var down = ReferenceFraction.FromDouble(result[2][i]!.Value);
            if (down.Sign != 0)
                result[3][i] = (up / down).ToDouble();
            result[4][i] = ((up - down) * (up - down)).ToDouble();
        }
        return result;
    }
}
