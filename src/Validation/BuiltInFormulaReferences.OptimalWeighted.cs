using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> OptimalWeightedOutputs(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); var result = new double[bars.Count];
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        for (var i = 0; i < prices.Length; i++)
        {
            var start = Math.Max(0, i - length + 1); var count = new ReferenceFraction(i - start + 1);
            var mx = new ReferenceFraction(0); var my = new ReferenceFraction(0);
            for (var j = start; j <= i; j++) { mx += prices[j]; my += ReferenceFraction.FromDouble(j == 0 ? 0 : result[j - 1]); }
            mx /= count; my /= count;
            var cross = new ReferenceFraction(0); var vx = new ReferenceFraction(0); var vy = new ReferenceFraction(0);
            for (var j = start; j <= i; j++)
            {
                var x = prices[j] - mx; var y = ReferenceFraction.FromDouble(j == 0 ? 0 : result[j - 1]) - my;
                cross += x * y; vx += x * x; vy += y * y;
            }
            var correlation = vx.Sign == 0 || vy.Sign == 0 ? 0 : cross.Sign * (cross * cross / (vx * vy)).SqrtToDouble();
            var top = new ReferenceFraction(0); var bottom = new ReferenceFraction(0);
            for (var j = 0; j < length; j++)
            {
                var weight = ReferenceFraction.FromDouble(Math.Pow(length - j, correlation)); bottom += weight;
                if (i >= j) top += prices[i - j] * weight;
            }
            result[i] = (top / bottom).ToDouble();
        }
        return Outputs(("Owma", result));
    }
}
