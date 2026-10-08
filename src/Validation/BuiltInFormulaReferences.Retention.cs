using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] RetentionValues(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length);
        ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var small = bars.Skip((int)Math.Max(0, i + 1L - length)).Take(Math.Min(i + 1, length)).ToArray();
            var large = bars.Skip((int)Math.Max(0, i + 1L - 2L * length)).Take((int)Math.Min(i + 1L, 2L * length)).ToArray();
            var h = R(small.Max(b => b.High)); var H = R(large.Max(b => b.High));
            var a = R(2) * (h - R(small.Min(b => b.Low)));
            var b = R(2) * (H - R(large.Min(b => b.Low)));
            double factor = 0;
            if (h.Sign > 0 && a.Sign != 0 && b.Sign != 0 && (a - R(1)).Sign != 0 && (b - R(1)).Sign != 0 && (a - b).Sign != 0)
            {
                var proportion = a / b;
                var radicand = proportion * proportion * H / h;
                factor = proportion.Sign > 0 && (radicand - R(1)).Sign >= 0 ? 1 : proportion.Sign * radicand.SqrtToDouble();
            }
            var weight = R(Math.Pow(factor, Math.Sqrt(length)) / length);
            var price = R(bars[i].Close); var previous = R(i == 0 ? bars[i].Close : result[i - 1]);
            result[i] = (price + (R(1) - weight) * (previous - price)).ToDouble();
        }
        return result;
    }
}

