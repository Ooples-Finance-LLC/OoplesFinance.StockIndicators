using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class PairStatisticsReference
{
    internal static double?[][] Calculate(
        IReadOnlyList<Bar> bars,
        int period,
        CandlePriceField left,
        CandlePriceField right,
        bool flatZero
    )
    {
        var output = Enumerable.Range(0, 5).Select(_ => new double?[bars.Count]).ToArray();
        double Field(Bar b, CandlePriceField f) =>
            f switch
            {
                CandlePriceField.Open => b.Open,
                CandlePriceField.High => b.High,
                CandlePriceField.Low => b.Low,
                _ => b.Close,
            };
        for (var i = period - 1; i < bars.Count; i++)
        {
            var values = bars.Skip(i - period + 1).Take(period).ToArray();
            var x = values.Select(b => ReferenceFraction.FromDouble(Field(b, left))).ToArray();
            var y = values.Select(b => ReferenceFraction.FromDouble(Field(b, right))).ToArray();
            var n = new ReferenceFraction(period);
            var mx = x.Aggregate(new ReferenceFraction(0), (s, v) => s + v) / n;
            var my = y.Aggregate(new ReferenceFraction(0), (s, v) => s + v) / n;
            var a = new ReferenceFraction(0);
            var b = a;
            var c = a;
            for (var j = 0; j < period; j++)
            {
                var dx = x[j] - mx;
                var dy = y[j] - my;
                a += dx * dx;
                b += dy * dy;
                c += dx * dy;
            }
            output[2][i] = (c / n).ToDouble();
            output[3][i] = (a / n).ToDouble();
            output[4][i] = (b / n).ToDouble();
            if (a.Sign == 0 || b.Sign == 0)
            {
                output[0][i] = output[1][i] = flatZero ? 0 : null;
                continue;
            }
            var squared = c * c / (a * b);
            output[0][i] = c.Sign * squared.SqrtToDouble();
            output[1][i] = squared.ToDouble();
        }
        return output;
    }
}
