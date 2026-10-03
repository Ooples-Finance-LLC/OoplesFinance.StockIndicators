using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] SurfaceRoughnessOutputs(IReadOnlyList<Bar> bars, int length = 100)
    {
        length = Math.Max(1, length); var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var first = Math.Max(0, i - length + 1); var n = new ReferenceFraction(i - first + 1);
            var x = Enumerable.Range(first, i - first + 1).Select(j => ReferenceFraction.FromDouble(j == 0 ? 0 : bars[j - 1].Close)).ToArray();
            var y = Enumerable.Range(first, i - first + 1).Select(j => ReferenceFraction.FromDouble(bars[j].Close)).ToArray();
            var xm = x.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / n; var ym = y.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / n;
            var xx = new ReferenceFraction(0); var yy = new ReferenceFraction(0); var xy = new ReferenceFraction(0);
            for (var j = 0; j < x.Length; j++) { var dx = x[j] - xm; var dy = y[j] - ym; xx += dx * dx; yy += dy * dy; xy += dx * dy; }
            var correlation = xx.Sign == 0 || yy.Sign == 0 ? 0 : xy.Sign * (xy * xy / (xx * yy)).SqrtToDouble();
            result[i] = 1 - ((correlation + 1) / 2);
        }
        return result;
    }
}
